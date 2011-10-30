/*   CandySpider.cs
 *   Author: Wendell
 *   Date Created: 10/27/2011
 *   
 *   Please email me at wendell.hack@gmail.com if you'd be adopting the code
 *   
 *   Thanks
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO.Ports;
using System.Threading;
using System.IO;

namespace CandySpider {
     /// <summary>
     /// Interaction logic for MainWindow.xaml
     /// </summary>
     public partial class MainWindow : Window {

          // XBEE explorer serial port
          private SerialPort _port = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One);

          DispatcherTimer _spiderTimer; // timer for the spider animation
          DispatcherTimer _spiderHitTimer; // timer for the spider hit animation
          DispatcherTimer _gameTimer; // main game timer
          DispatcherTimer _timeIsUpTimer; // just some "timer to blink the time is up text"
          int _animInterval = 5, // animation frame interval 
               _hitAnimInterval = 500, // time when the spider's eyes are in "ouch" mode
               _gameInterval = 1000; // 1 second ticks
          int _maxTicks = 80, // ticks for the spider animation
               _maxGameTicks = 30, // max game time
               _gameTicks = 30, // how many seconds
               _maxSpiders = 100; // cap for max spiders hit
          double _spiderStep = 0; // spider animation translate count
          bool _goingDown = true, // is spider going down 
               _gameStarted = false; // is game started
          Image spider; // spider animation
          Uri spiderURI, spiderOOURI; // location for spider sprites resource
          BitmapImage spiderImage, spiderOOImage; // loaded spider sprites
          Thread _listenerThread; // thread to listen for XBEE serial inputs
          bool timeIsUpAnimationFlag=false; // is time is up

          bool _isHit = false; // tells if a spider was hit (from serial input or button)
          int _spidersCaught = 0; // counts spiders


          public MainWindow() {
               InitializeComponent();
               // get the resources and load the bitmaps for the spider animation
               spiderURI = new Uri(@"/CandySpider;component/spider.png", UriKind.Relative);
               spiderImage = new BitmapImage(spiderURI);
               spiderOOURI = new Uri(@"/CandySpider;component/spiderOO.png", UriKind.Relative);
               spiderOOImage = new BitmapImage(spiderOOURI);

               // initialize the spider animation
               this.spider = new Image();
               // lay out in canvas first but initialized in code instead because of sprites
               //<Image Height="417" HorizontalAlignment="Left" Name="spider" Stretch="Fill" VerticalAlignment="Top" Width="533" />
               this.spider.Height = 417;
               this.spider.HorizontalAlignment = HorizontalAlignment.Left;
               this.spider.Name = "spider";
               this.spider.Stretch = Stretch.Fill;
               this.spider.VerticalAlignment = VerticalAlignment.Top;
               this.spider.Width = 533;
               this.spider.Source = spiderOOImage;
               this.drawArea.Children.Add(this.spider); // add to canvas so it will be easier to translate (Canvas.Set/Get)

               // initialize the spider anumation timer
               this._spiderTimer = new DispatcherTimer();
               this._spiderTimer.Interval = new TimeSpan(0, 0, 0, 0, this._animInterval);
               // perform the necessary actions per tick
               this._spiderTimer.Tick += new EventHandler(delegate(object sender, EventArgs e) {
                    this.AnimateSpider(); // animate spider!
               });
               this._spiderTimer.Start(); // start immediately

               // this is just for the "ouch" animation of the spider; dont start yet
               this._spiderHitTimer = new DispatcherTimer();
               this._spiderHitTimer.Interval = new TimeSpan(0, 0, 0, 0, this._hitAnimInterval);
               this._spiderHitTimer.Tick += new EventHandler(delegate(object sender, EventArgs e) {
                    this.ResetSpider();
               });

               // initialize the main game timer but dont start yet
               this._gameTimer = new DispatcherTimer();
               this._gameTimer.Interval = new TimeSpan(0, 0, 0, 0, this._gameInterval);
               this._gameTimer.Tick += new EventHandler(delegate(object sender, EventArgs e) {
                    this.GameCounter(); // starts game counter
               });

               // initialize the time is up text to blink always
               // initially the text is hidden
               this._timeIsUpTimer = new DispatcherTimer();
               this._timeIsUpTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
               this._timeIsUpTimer.Tick += new EventHandler(delegate(object sender, EventArgs e) {
                    // just switch between 2 colors
                    if (timeIsUpAnimationFlag) {
                         timeIsUp.Foreground = Brushes.Yellow;
                         timeIsUpAnimationFlag = false;
                    }
                    else {
                         timeIsUp.Foreground = Brushes.Red;
                         timeIsUpAnimationFlag = true;
                    }
               });
               this._timeIsUpTimer.Start(); // start immediately

               // initialize the game
               ResetGame();
               
               // start the listener thread for the XBEE input
               _listenerThread = new Thread(new ParameterizedThreadStart(delegate(object o) {
                    ListenPort();
               }));
               _listenerThread.Start(); // start
          }

          /// <summary>
          /// Method that constantly listens to the serial input stream
          /// </summary>
          private void ListenPort() {
               // initialize the sensor port, mine was registered as COM3, you may check yours
               // through the hardware devices from control panel
               int bytesToRead = 0;
               string chunk, message;
               _port.Open();
               try {
                    bool start;
                    start = false;
                    message = "";
                    while (true) {
                         // check if there are bytes incoming
                         bytesToRead = _port.BytesToRead;
                         if (bytesToRead > 0) {
                              byte[] input = new byte[bytesToRead];
                              // read the Xbee's input
                              _port.Read(input, 0, bytesToRead);
                              // convert the bytes into string
                              chunk = System.Text.Encoding.UTF8.GetString(input);
                              if (chunk.IndexOf("[") >= 0) {
                                   start = true;
                              }
                              if (start) {
                                   message += chunk;
                              }
                              if (chunk.IndexOf("]") >= 0) {
                                   start = false;
                                   // clean up code
                                   message = message.Trim().Replace("[", "").Replace("]", "");
                                   // the message expects "[hit]" but just removed the brackets
                                   if ("hit".Equals(message)) {
                                        Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
                                        {
                                             HitSpider();
                                        }));
                                   }
                                   message = "";
                              }
                              Console.WriteLine("");
                         }
                    }

               }
               finally {
                    // again always close the serial ports!
                    _port.Close();
               }
          }

          /// <summary>
          /// Main game counter method, it updates the time display and starts/stop the game
          /// </summary>
          private void GameCounter() {
               if (_gameStarted) {
                    if (_gameTicks > 0) {
                         _gameTicks--; // count down
                    }
                    time.Content = string.Format("Time: {0}s", _gameTicks.ToString("00"));
                    if (_gameTicks == 0) {
                         _gameStarted = false; // stop the game
                         _gameTimer.Stop(); // stop the game timer
                         timeIsUp.Visibility = System.Windows.Visibility.Visible; // show the "time is up" text 
                         //ResetGame();
                    }
               }
          }

          /// <summary>
          /// Animates the spider to move up and down
          /// </summary>
          protected void AnimateSpider() {
               TranslateTransform move;
               if (_goingDown) {
                    // translate down
                    move = new TranslateTransform(0, this._spiderStep);
               }
               else {
                    // translate up
                    move = new TranslateTransform(0, this._spiderStep);
               }
               // check if moving down or up
               if (_goingDown) {
                    _spiderStep++;
               }
               else {
                    _spiderStep--;
               }
               // then set if to go down or up next
               if (_spiderStep > _maxTicks) {
                    _goingDown = false;
               }
               if (_spiderStep == 0) {
                    _goingDown = true;
               }
               // apply the movement
               this.spider.RenderTransform = move;
          }

          /// <summary>
          /// Resets the spider's expression to round eyes
          /// </summary>
          private void ResetSpider() {
               if (!_isHit) {
                    this.spider.Source = spiderOOImage;
                    this._spiderHitTimer.Stop(); 
                    return;
               }
               _isHit = false;
          }

          /// <summary>
          /// Changes the spider's expression to ouch and also updates the number of spiders hit
          /// </summary>
          protected void HitSpider() {
               if (_gameStarted) {
                    if (!_isHit) {
                         _isHit = true;
                         this.spider.Source = spiderImage; // ouch eyes
                         this._spiderHitTimer.Start();
                    }
                    // cap for max spiders, also increments
                    if (++_spidersCaught >= _maxSpiders) {
                         return;
                    }
                    // update the display counter
                    hitCount.Content = string.Format("{0}", _spidersCaught.ToString("00"));
               }
          }

          /// <summary>
          /// Resets the game
          /// </summary>
          private void ResetGame() {
               // the button does not disappear anyway
               start.Visibility = System.Windows.Visibility.Visible;
               _gameStarted = false; // signal game start
               // reset counters
               _spidersCaught = 0; 
               hitCount.Content = string.Format("00");
               _gameTicks = _maxGameTicks;
               time.Content = string.Format("Time: {0}s", _gameTicks.ToString("00"));
               // reset game timer
               _gameTimer.Stop();
               timeIsUp.Visibility = System.Windows.Visibility.Hidden;
          }

          /// <summary>
          /// Starts the game!
          /// </summary>
          private void StartGame() {
               if (!_gameStarted) {
                    // button does not hide anyway
                    start.Visibility = System.Windows.Visibility.Hidden;
                    ResetGame(); // reset the game
                    _gameStarted = true;
                    _gameTimer.Start(); // start the game timer
               }
          }

          /// <summary>
          /// Some button to simulate the spider hit
          /// </summary>
          /// <param name="sender"></param>
          /// <param name="e"></param>
          private void rawr_Click(object sender, RoutedEventArgs e) {
               HitSpider();
          }

          /// <summary>
          /// Resets the game
          /// </summary>
          /// <param name="sender"></param>
          /// <param name="e"></param>
          private void reset_Click(object sender, RoutedEventArgs e) {
               ResetGame();
          }


          /// <summary>
          /// Starts a game
          /// </summary>
          /// <param name="sender"></param>
          /// <param name="e"></param>
          private void start_Click(object sender, RoutedEventArgs e) {
               StartGame();
          }

     }
}
