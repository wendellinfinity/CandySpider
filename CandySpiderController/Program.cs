/*   Program.cs
 *   Author: Wendell
 *   Date Created: 10/27/2011
 *   
 *   Please email me at wendell.hack@gmail.com if you'd be adopting the code
 *   
 *   Thanks
 */

using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using System.IO.Ports;

namespace CandySpiderController {
     public class Program {

          // serial port to be user be XBEE to send data
          static SerialPort _serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
          static OutputPort[] _spiders; // LED ports, used the analog marked ports for these
          //static OutputPort _hitIndicator;
          static InterruptPort[] _hits; // the switch input ports, used digital marked ports on netduino
          static Random _random; // random generator
          static bool[] _states; // keeps states for each LED
          static Thread[] _runners; // runners for each LED to flash randomly
          public static void Main() {
               //_hitIndicator = new OutputPort(Cpu.Pin.GPIO_Pin13, false);
               _random = new Random(); // initialize random
               // initialize the LED ports
               _spiders = new OutputPort[6] { 
                    new OutputPort(Pins.GPIO_PIN_A0, false),
                    new OutputPort(Pins.GPIO_PIN_A1, false),
                    new OutputPort(Pins.GPIO_PIN_A2, false),
                    new OutputPort(Pins.GPIO_PIN_A3, false),
                    new OutputPort(Pins.GPIO_PIN_A4, false),
                    new OutputPort(Pins.GPIO_PIN_A5, false)
               };
               // initialize LED port states
               _states = new bool[6] { false, false, false, false, false, false };
               // initialize the Interrupt ports for the input, only detect when pressed on (signal to HIGH)
               _hits = new InterruptPort[6] { 
                    new InterruptPort(Pins.GPIO_PIN_D3,true,ResistorModes.Disabled,InterruptModes.InterruptEdgeLevelHigh),
                    new InterruptPort(Pins.GPIO_PIN_D4,true,ResistorModes.Disabled,InterruptModes.InterruptEdgeLevelHigh),
                    new InterruptPort(Pins.GPIO_PIN_D5,true,ResistorModes.Disabled,InterruptModes.InterruptEdgeLevelHigh),
                    new InterruptPort(Pins.GPIO_PIN_D6,true,ResistorModes.Disabled,InterruptModes.InterruptEdgeLevelHigh),
                    new InterruptPort(Pins.GPIO_PIN_D7,true,ResistorModes.Disabled,InterruptModes.InterruptEdgeLevelHigh),
                    new InterruptPort(Pins.GPIO_PIN_D8,true,ResistorModes.Disabled,InterruptModes.InterruptEdgeLevelHigh)
               };
               // open the serialport
               _serialPort.Open();
               // loop through the input ports
               for (int i = 0; i < 6; i++) {
                    // assign interrupt event delegate on each of the input ports
                    _hits[i].OnInterrupt += new NativeEventHandler(
                         delegate(uint data1, uint data2, DateTime time) {
                              Thread runner = null; // just to contain the current thread
                              bool hasHit = false;
                              OutputPort spider = null; // just to contain the matching LED for the switch
                              for (int p = 0; p < 6; p++) {
                                   // get the matching pin from the interrupt
                                   if ((uint)_hits[p].Id == data1) {
                                        _hits[p].ClearInterrupt(); // clear the interrupt
                                        // then get the associated thread for the LED and the Outputport
                                        switch (data1) {
                                             case (uint)Pins.GPIO_PIN_D3:
                                                  runner = _runners[5];
                                                  spider = _spiders[5];
                                                  break;
                                             case (uint)Pins.GPIO_PIN_D4:
                                                  runner = _runners[4];
                                                  spider = _spiders[4];
                                                  break;
                                             case (uint)Pins.GPIO_PIN_D5:
                                                  runner = _runners[3];
                                                  spider = _spiders[3];
                                                  break;
                                             case (uint)Pins.GPIO_PIN_D6:
                                                  runner = _runners[2];
                                                  spider = _spiders[2];
                                                  break;
                                             case (uint)Pins.GPIO_PIN_D7:
                                                  runner = _runners[1];
                                                  spider = _spiders[1];
                                                  break;
                                             case (uint)Pins.GPIO_PIN_D8:
                                                  runner = _runners[0];
                                                  spider = _spiders[0];
                                                  break;
                                             default:
                                                  break;
                                        }
                                   }
                              }
                              // if the output port is HIGH (lighted up)
                              // and this interrupt says the switch is hit...
                              if (spider.Read()) {
                                   // just flag (simpler too if I just moved the next if block below here
                                   hasHit = true;
                              }
                              // check the flag, i can just move this code above, but i was thinking of doing something...
                              if (hasHit) {
                                   // suspend the current thread that runs the LED light
                                   runner.Suspend();
                                   spider.Write(false); // set the LED to LOW (off)
                                   SendSerial("[hit]"); // send serial through XBEE
                                   Debug.Print(data1.ToString() + " : " + data2.ToString());
                              }
                         });
               }
               // initialize the threads for the randomly lighting LEDs
               _runners = new Thread[6];
               for (int t = 0; t < 6; t++) {
                    OutputPort spider = _spiders[t]; // just the current LED in the loop for use of delegate
                    _runners[t] = new Thread(delegate() {
                         int enable, duration;
                         // loop endlessly (but not)
                         while (true) {
                              // get if the LED will light up
                              enable = _random.Next(2);
                              if (enable > 0) {
                                   spider.Write(true); // on
                              }
                              else {
                                   spider.Write(false); // off
                              }
                              // randomize how long it will light up
                              duration = _random.Next(5);
                              // the max will be 1.5 seconds but, fastest is 500ms
                              System.Threading.Thread.Sleep((duration*200)+500);
                              // then turn off the LED after (this is an opportunity for the interrupt to happen)
                              spider.Write(false); 
                              System.Threading.Thread.Sleep(800); // sleep the thread
                              System.Threading.Thread.CurrentThread.Suspend();
                         }
                    });
                    _runners[t].Start(); // start to light them up!
               }
               // this loop just wakes up all of the sleeping LEDs so that they will light up again
               // when an interrupt took it off or the LED was not hit but turned off
               while (true) {
                    for (int t = 0; t < 6; t++) {
                         if (_runners[t].ThreadState != ThreadState.Running) {
                              _runners[t].Resume();
                         }
                    }
               }
          }

          /// <summary>
          /// Sends message to serial port
          /// </summary>
          /// <param name="message"></param>
          private static void SendSerial(string message) {
               int bytesToRead = _serialPort.BytesToRead;
               byte[] buffer = new byte[message.Length];
               if (bytesToRead > 0) {
                    // get the waiting data
                    byte[] bufferIn = new byte[bytesToRead];
                    // READ any data received
                    _serialPort.Read(bufferIn, 0, bufferIn.Length);
               }
               buffer = System.Text.Encoding.UTF8.GetBytes(message);
               _serialPort.Write(buffer, 0, buffer.Length);
          }

     }
}
