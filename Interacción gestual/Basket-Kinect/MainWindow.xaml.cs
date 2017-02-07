//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.BodyBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Text;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged {

        /// <summary>
        /// Brush used for drawing reached goals
        /// </summary>
        private readonly Brush goalReachedBrush = new SolidColorBrush(Color.FromArgb(64, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing not reached goals
        /// </summary>
        private readonly Brush goalNotReachedBrush = new SolidColorBrush(Color.FromArgb(64, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing gesture line
        /// </summary>
        private readonly Brush gestureLineBrush = new SolidColorBrush(Color.FromArgb(150, 255, 215, 0));

        /// <summary>
        /// Brush used for drawing gesture points
        /// </summary>
        private readonly Brush gesturePointBrush = new SolidColorBrush(Color.FromArgb(150, 255, 165, 0));

        /// <summary>
        /// True if gesture start is correct, false otherwise
        /// </summary>
        private bool beginShot = false;

        /// <summary>
        /// True if gesture end is correct, false otherwise
        /// </summary>
        private bool endShot = false;

        /// <summary>
        /// Floor center: coordinate X
        /// </summary>
        private const double FloorCenterX = 0.0;

        /// <summary>
        /// Floor center: coordinate Y
        /// </summary>
        private const double FloorCenterY = -1.0;

        /// <summary>
        /// Floor center: coordinate Z
        /// </summary>
        private const double FloorCenterZ = 2.5;

        /// <summary>
        /// User head height
        /// </summary>
        private double HeadHeight = -10;

        /// <summary>
        /// True if user has the arrow to shot it, false otherwise
        /// </summary>
        private bool ballInHand = false;

        /// <summary>
        /// Radius of concentric circles
        /// </summary>
        private double[] DartBoardRadius = new double[5];

        /// <summary>
        /// True if user selected if he is left or right handed, false otherwise
        /// </summary>
        private bool userSelectedHand = false;

        /// <summary>
        /// True if user selected the distance to shoot
        /// </summary>
        private bool selectedDistance = false;

        /// <summary>
        /// True if user is right handed, false otherwise
        /// </summary>
        private bool rightHanded = false;

        /// <summary>
        /// User puntuation
        /// </summary>
        private int userTotalPuntuation = 0;

        /// <summary>
        /// User puntuation if he shots in that moment
        /// </summary>
        private int userPointsIfShots = 1;

        /// <summary>
        /// Initial and final point to shot
        /// </summary>
        Joint iniPosition = new Joint();
        Joint finalPosition = new Joint();

        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush HandOpenBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Initial milliseconds
        /// </summary>
        long currentMilliseconds;

        /// <summary>
        /// Real gameplay time
        /// </summary>
        float time;

        /// <summary>
        /// True if we have started to play on the dartboard, false otherwise
        /// </summary>
        bool activateTime = true;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow() {

            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            //Initialize iniPosition
            iniPosition.Position.X = 0;
            iniPosition.Position.Y = 0;
            iniPosition.Position.Z = 0;
            finalPosition.Position.X = 0;
            finalPosition.Position.Y = 0;
            finalPosition.Position.Z = 0;

            //Initialize time different to 0
            time = 10;

        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// Código externo
        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource {
            get {
                return this.imageSource;
            }
        }

        /// Código externo
        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText{ 
            get {
                return this.statusText;
            }

            set {
                if (this.statusText != value) {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null) {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// Código externo
        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e) {

            if (this.bodyFrameReader != null)
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            
        }

        /// Código externo
        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e) {

            if (this.bodyFrameReader != null) {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null) {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        ///Código propio
        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e) {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame()) {

                if (bodyFrame != null) {

                    if (this.bodies == null)
                        this.bodies = new Body[bodyFrame.BodyCount];
                    
                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived) {

                using (DrawingContext dc = this.drawingGroup.Open()) {

                    // Draw a transparent background to set the render size
                    System.String bgPath = Path.GetFullPath(@"..\..\..\Images\menu.jpg");
                    ImageBrush imageBg = new ImageBrush(new BitmapImage(new Uri(bgPath)));
                    dc.DrawRectangle(imageBg, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                    
                    int penIndex = 0;

                    // Flag to detect only one body
                    Boolean firstBody = false;
                    foreach (Body body in this.bodies) {

                        Pen drawPen = this.bodyColors[penIndex++];

                        if (body.IsTracked && firstBody == false) {
                            firstBody = true;

                            this.DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys) {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0) {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }
                            

                            // While position is not correct, draw floor
                            if (HeadHeight == -10)
                            {
                                this.DrawBody(joints, jointPoints, dc, drawPen);
                                HeadHeight = this.DrawFloorPoint(joints[JointType.FootLeft], joints[JointType.FootRight], joints[JointType.Head], dc);

                            }

                            //Select hand
                            else if (!userSelectedHand)
                            {

                                string handsAdvice = "Elige diestro para lanzar con la mano derecha.\n" +
                                                     "Elige zurdo para lanzar con la mano izquierda.";
                                FormattedText floorAdviceText = new FormattedText(handsAdvice, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Arial Black"), 17, Brushes.White);
                                dc.DrawText(floorAdviceText, new Point(10, 20));
                                this.DrawBody(joints, jointPoints, dc, drawPen);

                                //User must select if he is left or right handed: Show buttons:
                                //Left handed
                                System.String leftPath = Path.GetFullPath(@"..\..\..\Images\zurdo.png");
                                if (jointPoints[JointType.HandLeft].X > 100 && jointPoints[JointType.HandLeft].X < (100 + 100) &&
                                    jointPoints[JointType.HandLeft].Y > 150 && jointPoints[JointType.HandLeft].Y < 200) {

                                    leftPath = Path.GetFullPath(@"..\..\..\Images\zurdopulsado.png");
                                }

                                BitmapImage leftImage = new BitmapImage();
                                leftImage.BeginInit();
                                leftImage.UriSource = new Uri(leftPath);
                                leftImage.EndInit();
                                dc.DrawImage(leftImage, new Rect(100, 125, 100, 125));

                                //Right handed
                                System.String rightPath = Path.GetFullPath(@"..\..\..\Images\diestro.png");
                                if (jointPoints[JointType.HandRight].X > (displayWidth - 200) && jointPoints[JointType.HandRight].X < ((displayWidth - 100) + 100) &&
                                    jointPoints[JointType.HandRight].Y > 150 && jointPoints[JointType.HandRight].Y < 200) {

                                    rightPath = Path.GetFullPath(@"..\..\..\Images\diestropulsado.png");
                                }

                                BitmapImage rightImage = new BitmapImage();
                                rightImage.BeginInit();
                                rightImage.UriSource = new Uri(rightPath);
                                rightImage.EndInit();
                                dc.DrawImage(rightImage, new Rect(displayWidth - 200, 125, 100, 125));

                                //Test if user closed his hand in a button:
                                //Left handed:
                                if (jointPoints[JointType.HandLeft].X > 100 && jointPoints[JointType.HandLeft].X < (100 + 100) &&
                                    jointPoints[JointType.HandLeft].Y > 150 && jointPoints[JointType.HandLeft].Y < 200 &&
                                    body.HandLeftState == HandState.Closed) {

                                    userSelectedHand = true;
                                    rightHanded = false;
                                }

                                //Right handed:
                                else if (jointPoints[JointType.HandRight].X > (displayWidth - 200) && jointPoints[JointType.HandRight].X < ((displayWidth - 200) + 100) &&
                                        jointPoints[JointType.HandRight].Y > 150 && jointPoints[JointType.HandRight].Y < 200 &&
                                        body.HandRightState == HandState.Closed) {

                                    userSelectedHand = true;
                                    rightHanded = true;
                                }

                                
                            }

                            //Select shooting type
                            else if (!selectedDistance) {

                                
                                string handsAdvice = "Elige la distancia de tiro\n";
                                FormattedText floorAdviceText = new FormattedText(handsAdvice, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Arial Black"), 17, Brushes.White);
                                dc.DrawText(floorAdviceText, new Point(10, 20));
                                this.DrawBody(joints, jointPoints, dc, drawPen);

                                //User must select the distance to shoot: Show buttons:
                                //Free throw
                                System.String leftPath = Path.GetFullPath(@"..\..\..\Images\tirolibre.png");
                                if ((jointPoints[JointType.HandRight].X > 50 && jointPoints[JointType.HandRight].X < 150 &&
                                    jointPoints[JointType.HandRight].Y > 125 && jointPoints[JointType.HandRight].Y < 250) || 
                                    (jointPoints[JointType.HandLeft].X > 50 && jointPoints[JointType.HandLeft].X < 150 &&
                                    jointPoints[JointType.HandLeft].Y > 125 && jointPoints[JointType.HandLeft].Y < 250)) {


                                    leftPath = Path.GetFullPath(@"..\..\..\Images\tirolibrepulsado.png");
                                }

                                BitmapImage leftImage = new BitmapImage();
                                leftImage.BeginInit();
                                leftImage.UriSource = new Uri(leftPath);
                                leftImage.EndInit();
                                dc.DrawImage(leftImage, new Rect(50, 125, 100, 125));

                                //Zone
                                System.String zonaPath = Path.GetFullPath(@"..\..\..\Images\zona.png");
                                if ((jointPoints[JointType.HandRight].X > 200 && jointPoints[JointType.HandRight].X < 300 &&
                                    jointPoints[JointType.HandRight].Y > 20 && jointPoints[JointType.HandRight].Y < 145) ||
                                    (jointPoints[JointType.HandLeft].X > 200 && jointPoints[JointType.HandLeft].X < 300 &&
                                    jointPoints[JointType.HandLeft].Y > 20 && jointPoints[JointType.HandLeft].Y < 145)) {

                                   zonaPath  = Path.GetFullPath(@"..\..\..\Images\zonapulsado.png");
                                }

                                BitmapImage restartImage = new BitmapImage();
                                restartImage.BeginInit();
                                restartImage.UriSource = new Uri(zonaPath);
                                restartImage.EndInit();
                                dc.DrawImage(restartImage, new Rect(200, 20, 100, 125));

                                //Three pointer
                                System.String rightPath = Path.GetFullPath(@"..\..\..\Images\triple.png");
                                if ((jointPoints[JointType.HandRight].X > 350 && jointPoints[JointType.HandRight].X < 450 &&
                                    jointPoints[JointType.HandRight].Y > 125 && jointPoints[JointType.HandRight].Y < 250) ||
                                    (jointPoints[JointType.HandLeft].X > 350 && jointPoints[JointType.HandLeft].X < 450 &&
                                    jointPoints[JointType.HandLeft].Y > 125 && jointPoints[JointType.HandLeft].Y < 250)) {

                                    rightPath = Path.GetFullPath(@"..\..\..\Images\triplepulsado.png");
                                }

                                BitmapImage rightImage = new BitmapImage();
                                rightImage.BeginInit();
                                rightImage.UriSource = new Uri(rightPath);
                                rightImage.EndInit();
                                dc.DrawImage(rightImage, new Rect(350, 125, 100, 125));
                                
                                //Test if user closed his hand in a button:
                                //Free throw:
                                if ((jointPoints[JointType.HandRight].X > 50 && jointPoints[JointType.HandRight].X < 150 &&
                                    jointPoints[JointType.HandRight].Y > 125 && jointPoints[JointType.HandRight].Y < 250 &&
                                    body.HandRightState == HandState.Closed) ||
                                    (jointPoints[JointType.HandLeft].X > 50 && jointPoints[JointType.HandLeft].X < 150 &&
                                    jointPoints[JointType.HandLeft].Y > 125 && jointPoints[JointType.HandLeft].Y < 250 &&
                                    body.HandLeftState == HandState.Closed))
                                {

                                    selectedDistance = true;
                                    userPointsIfShots = 1;
                                }

                                //Zone:
                                else if ((jointPoints[JointType.HandRight].X > 200 && jointPoints[JointType.HandRight].X < 300 &&
                                    jointPoints[JointType.HandRight].Y > 20 && jointPoints[JointType.HandRight].Y < 145 &&
                                    body.HandRightState == HandState.Closed) ||
                                    (jointPoints[JointType.HandLeft].X > 200 && jointPoints[JointType.HandLeft].X < 300 &&
                                    jointPoints[JointType.HandLeft].Y > 20 && jointPoints[JointType.HandLeft].Y < 145 &&
                                    body.HandLeftState == HandState.Closed)) {

                                    selectedDistance = true;
                                    userPointsIfShots = 2;
                                }

                                //Three pointer:
                                else if ((jointPoints[JointType.HandRight].X > 350 && jointPoints[JointType.HandRight].X < 450 &&
                                    jointPoints[JointType.HandRight].Y > 125 && jointPoints[JointType.HandRight].Y < 250 &&
                                    body.HandRightState == HandState.Closed) ||
                                    (jointPoints[JointType.HandLeft].X > 350 && jointPoints[JointType.HandLeft].X < 450 &&
                                    jointPoints[JointType.HandLeft].Y > 125 && jointPoints[JointType.HandLeft].Y < 250 &&
                                    body.HandLeftState == HandState.Closed))
                                {

                                    selectedDistance = true;
                                    userPointsIfShots = 3;
                                }

                                //Active time
                                if (activateTime) {

                                    //Initial milliseconds, 30 seconds from now
                                    currentMilliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond + (long)30000;
                                    activateTime = false;
                                }
                            }

                            //User positioned and options selected: start the game
                            else if (time > 0) {

                                // Draw a transparent background to set the render size
                                bgPath = Path.GetFullPath(@"..\..\..\Images\canasta.jpg");
                                imageBg = new ImageBrush(new BitmapImage(new Uri(bgPath)));
                                dc.DrawRectangle(imageBg, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                                // Draw the body
                                this.DrawBody(joints, jointPoints, dc, drawPen);
                              
                                // Time count
                                long milliseconds = currentMilliseconds - DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                                time = (float)milliseconds / 1000;
                                
                                //Set the inicial and final positions of the shoot
                                if (rightHanded) {

                                    //Shooting inicial position
                                    iniPosition.Position.X = (float)FloorCenterX + 0.3f;
                                    iniPosition.Position.Y = (float)HeadHeight - 0.2f;
                                    iniPosition.Position.Z = (float)FloorCenterZ + 0.1f;

                                    //Shooting final position
                                    finalPosition.Position.X = (float)FloorCenterX + 0.3f;

                                    if (userPointsIfShots == 1)
                                        finalPosition.Position.Y = (float)HeadHeight + 0.1f;
                                    else if (userPointsIfShots == 2)
                                        finalPosition.Position.Y = (float)HeadHeight + 0.15f;
                                    else
                                        finalPosition.Position.Y = (float)HeadHeight + 0.2f;

                                    finalPosition.Position.Z = (float)FloorCenterZ - 0.15f;
                                }

                                else {
                                    
                                    //Shooting inicial position
                                    iniPosition.Position.X = (float)FloorCenterX - 0.3f;
                                    iniPosition.Position.Y = (float)HeadHeight - 0.2f;
                                    iniPosition.Position.Z = (float)FloorCenterZ + 0.1f;

                                    //Shooting final position
                                    finalPosition.Position.X = (float)FloorCenterX - 0.3f;

                                    if (userPointsIfShots == 1)
                                        finalPosition.Position.Y = (float)HeadHeight + 0.1f;
                                    else if (userPointsIfShots == 2)
                                        finalPosition.Position.Y = (float)HeadHeight + 0.15f;
                                    else
                                        finalPosition.Position.Y = (float)HeadHeight + 0.2f;

                                    finalPosition.Position.Z = (float)FloorCenterZ - 0.15f;
                                }

                                string timeStr = time.ToString("#.#");

                                //Showing user points and time:
                                FormattedText userPointsTimeText = new FormattedText("Puntos: " + userTotalPuntuation + "    Tiempo: " + timeStr, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Arial Black"), 28, Brushes.Gold);
                                dc.DrawText(userPointsTimeText, new Point(20, 10));

                                //User has NOT the ball
                                if (!ballInHand) {

                                    string ballAdvice = "Coge la pelota: \n estira la mano hacia delante y abajo";
                                    FormattedText floorAdviceText = new FormattedText(ballAdvice, CultureInfo.GetCultureInfo("en-us"),
                                    FlowDirection.LeftToRight, new Typeface("Arial Black"), 30, Brushes.Black);
                                    dc.DrawText(floorAdviceText, new Point(20, this.displayHeight - 120));

                                    //Show that user has NOT the ball
                                    System.String noballPath = Path.GetFullPath(@"..\..\..\Images\noBall.png");
                                    BitmapImage noballImage = new BitmapImage();
                                    noballImage.BeginInit();
                                    noballImage.UriSource = new Uri(noballPath);
                                    noballImage.EndInit();
                                    dc.DrawImage(noballImage, new Rect(this.displayWidth - 80, 10, 80, 80));

                                    //User has to take a ball to shot it
                                    if (rightHanded)
                                        ballInHand = GetBall(joints[JointType.HandRight], joints[JointType.SpineMid], dc);
                                    else
                                        ballInHand = GetBall(joints[JointType.HandLeft], joints[JointType.SpineMid], dc);
                                }

                                //User start shooting
                                else if (!beginShot) {

                                    //Show that user has the ball
                                    System.String ballPath = Path.GetFullPath(@"..\..\..\Images\ball.png");
                                    BitmapImage ballImage = new BitmapImage();
                                    ballImage.BeginInit();
                                    ballImage.UriSource = new Uri(ballPath);
                                    ballImage.EndInit();
                                    dc.DrawImage(ballImage, new Rect(this.displayWidth - 80, 10, 80, 80));

                                    String showAdvice = "Prepárate para lanzar!";
                                    FormattedText floorAdviceText = new FormattedText(showAdvice, CultureInfo.GetCultureInfo("en-us"),
                                    FlowDirection.LeftToRight, new Typeface("Arial Black"), 25, Brushes.Black);
                                    dc.DrawText(floorAdviceText, new Point(20, this.displayHeight - 100));

                                    if (rightHanded)
                                        beginShot = this.DrawGesturePoint(iniPosition, joints[JointType.HandRight], dc);

                                    else
                                        beginShot = this.DrawGesturePoint(iniPosition, joints[JointType.HandLeft], dc);

                                }
                                
                                //User realize a shoot
                                else {

                                    String showAdvice = "Lanza la pelota!";
                                    FormattedText floorAdviceText = new FormattedText(showAdvice, CultureInfo.GetCultureInfo("en-us"),
                                    FlowDirection.LeftToRight, new Typeface("Arial Black"), 25, Brushes.Black);
                                    dc.DrawText(floorAdviceText, new Point(20, this.displayHeight - 100));

                                    if(rightHanded)
                                        endShot = this.DrawGesturePoint(finalPosition, joints[JointType.HandRight], dc);
                                    else
                                        endShot = this.DrawGesturePoint(finalPosition, joints[JointType.HandLeft], dc);
                                }

                                if (endShot) {

                                    ballInHand = false;

                                    //Calculate shot points:
                                    userTotalPuntuation += userPointsIfShots;
                                    beginShot = false;
                                    endShot = false;

                                    //Sum up seconds depending on the puntuation
                                    currentMilliseconds += (long)userPointsIfShots * 1000 / 3;
                                }
                            }

                            // If time is over
                            else {

                                bgPath = Path.GetFullPath(@"..\..\..\Images\gameover.png");
                                imageBg = new ImageBrush(new BitmapImage(new Uri(bgPath)));
                                dc.DrawRectangle(imageBg, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                               
                                //Showing user points:
                                FormattedText userPointsText = new FormattedText("Puntuación Final: " + userTotalPuntuation, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Arial Black"), 36, Brushes.White);
                                dc.DrawText(userPointsText, new Point(70, 250));

                                this.DrawBody(joints, jointPoints, dc, drawPen);

                                //User must select if he wants to try again or exit
                                System.String restartPath = Path.GetFullPath(@"..\..\..\Images\tryagain.png");

                                if ((jointPoints[JointType.HandRight].X > 100 && jointPoints[JointType.HandRight].X < 200 &&
                                    jointPoints[JointType.HandRight].Y > 20 && jointPoints[JointType.HandRight].Y < 145) ||
                                    (jointPoints[JointType.HandLeft].X > 200 && jointPoints[JointType.HandLeft].X < 200 &&
                                    jointPoints[JointType.HandLeft].Y > 20 && jointPoints[JointType.HandLeft].Y < 145)) {

                                    restartPath = Path.GetFullPath(@"..\..\..\Images\tryagainpulsado.png");
                                }

                                BitmapImage restartImage = new BitmapImage();
                                restartImage.BeginInit();
                                restartImage.UriSource = new Uri(restartPath);
                                restartImage.EndInit();
                                dc.DrawImage(restartImage, new Rect(100, 20, 100, 125));

                                System.String exitPath = Path.GetFullPath(@"..\..\..\Images\exit.png");

                                if ((jointPoints[JointType.HandRight].X > (displayWidth - 200) && jointPoints[JointType.HandRight].X < (displayWidth - 100) &&
                                   jointPoints[JointType.HandRight].Y > 20 && jointPoints[JointType.HandRight].Y < 145) ||
                                   (jointPoints[JointType.HandLeft].X > (displayWidth - 200) && jointPoints[JointType.HandLeft].X < (displayWidth - 100) &&
                                   jointPoints[JointType.HandLeft].Y > 20 && jointPoints[JointType.HandLeft].Y < 145)) {

                                    exitPath = Path.GetFullPath(@"..\..\..\Images\exitpulsado.png");
                                }

                                BitmapImage exitImage = new BitmapImage();
                                exitImage.BeginInit();
                                exitImage.UriSource = new Uri(exitPath);
                                exitImage.EndInit();
                                dc.DrawImage(exitImage, new Rect(displayWidth-200, 20, 100, 125));

                                //Test if user closed his hand in restart button:
                                if ((jointPoints[JointType.HandRight].X > 100 && jointPoints[JointType.HandRight].X < 200 &&
                                    jointPoints[JointType.HandRight].Y > 20 && jointPoints[JointType.HandRight].Y < 145 &&
                                    body.HandRightState == HandState.Closed) ||
                                    (jointPoints[JointType.HandLeft].X > 100 && jointPoints[JointType.HandLeft].X < 200 &&
                                    jointPoints[JointType.HandLeft].Y > 20 && jointPoints[JointType.HandLeft].Y < 145 &&
                                    body.HandLeftState == HandState.Closed)) {

                                    //Restart the variables
                                    activateTime = true;
                                    userSelectedHand = false;
                                    selectedDistance = false;
                                    ballInHand = false;
                                    userTotalPuntuation = 0;
                                    time = 10;
                                }


                                //Test if user closed his hand in exit button:
                                if ((jointPoints[JointType.HandRight].X > (displayWidth - 200) && jointPoints[JointType.HandRight].X < (displayWidth - 100) &&
                                   jointPoints[JointType.HandRight].Y > 20 && jointPoints[JointType.HandRight].Y < 145 &&
                                   body.HandRightState == HandState.Closed) ||
                                   (jointPoints[JointType.HandLeft].X > (displayWidth - 200) && jointPoints[JointType.HandLeft].X < (displayWidth - 100) &&
                                   jointPoints[JointType.HandLeft].Y > 20 && jointPoints[JointType.HandLeft].Y < 145 &&
                                   body.HandLeftState == HandState.Closed)) {

                                    //Exit
                                    Close();
                                }
                            }
                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                }
            }
        }

        /// Código externo
        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen) {
            // Draw the bones
            foreach (var bone in this.bones)
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            

            // Draw the joints
            foreach (JointType jointType in joints.Keys) {

                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                    drawBrush = this.trackedJointBrush;
                
                else if (trackingState == TrackingState.Inferred)
                    drawBrush = this.inferredJointBrush;
                
                if (drawBrush != null)
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
            }
        }

        /// Código externo
        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen) {

            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked || joint1.TrackingState == TrackingState.NotTracked)
                return;
          
            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
                drawPen = drawingPen;
            
            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }
        

        /// Código propio
        /// <summary>
        /// Recognise if user takes a ball
        /// </summary>
        /// <param name="userHand">user hand</param>
        /// <param name="userSpineMid">user spine mid</param>
        /// <param name="drawingContext">drawig context to draw</param>
        bool GetBall(Joint userHand, Joint userSpineMid, DrawingContext drawingContext) {

            if (userHand.Position.Y < userSpineMid.Position.Y-0.1f &&
                userHand.Position.Z < userSpineMid.Position.Z) 
                return true;
           
            return false;

        }
        
        /// Código propio
        /// <summary>
        /// Draws an ellipse in the floor goal
        /// </summary>
        /// <param name="foot_left">position of the left foot</param>
        /// /// <param name="foot_right">position of the right foot</param>
        /// <param name="head">position of the head</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private double DrawFloorPoint(Joint foot_left, Joint foot_right, Joint head, DrawingContext drawingContext) {

            Joint center_floor = new Joint();
            center_floor.Position.X = (float)FloorCenterX;
            center_floor.Position.Y = (float)FloorCenterY;
            center_floor.Position.Z = (float)FloorCenterZ;

            // Transform world coordinates to screen coordinates
            DepthSpacePoint depth_center_floor = this.coordinateMapper.MapCameraPointToDepthSpace(center_floor.Position);
            Point center_floor_2D = new Point(depth_center_floor.X, depth_center_floor.Y);

            // If the user is in the good floor position, the ellipse change it color to green
            if ((foot_left.Position.X < FloorCenterX + 0.3 && foot_left.Position.X > FloorCenterX - 0.3) &&
                (foot_right.Position.X < FloorCenterX + 0.3 && foot_right.Position.X > FloorCenterX - 0.3) &&
                (foot_left.Position.Y < FloorCenterY + 0.3 && foot_left.Position.Y > FloorCenterY - 0.3) &&
                (foot_right.Position.Y < FloorCenterY + 0.3 && foot_right.Position.Y > FloorCenterY - 0.3) &&
                (foot_left.Position.Z < FloorCenterZ + 0.3 && foot_left.Position.Z > FloorCenterZ - 0.3) &&
                (foot_right.Position.Z < FloorCenterZ + 0.3 && foot_right.Position.Z > FloorCenterZ - 0.3)) {

                drawingContext.DrawEllipse(Brushes.Red, null, center_floor_2D, 24, 8);
                // Return the user's height
                return head.Position.Y;
            }

            // If not, the ellipse change it color to red
            else {
                //Showing advices to positioning
                string floorAdvice = "";
                if (head.Position.X < FloorCenterX ) {

                    if (head.Position.Z < FloorCenterZ) 
                        floorAdvice = "Muévete hacia la derecha y \n hacia atrás";
                   
                    else if (head.Position.Z > FloorCenterZ) 
                        floorAdvice = "Muévete hacia la derecha y \n hacia delante";
                }

                else if (head.Position.X > FloorCenterX) {

                    if (head.Position.Z < FloorCenterZ) 
                        floorAdvice = "Muévete hacia la izquierda y \n hacia atrás";
                    
                    else if (head.Position.Z > FloorCenterZ) 
                        floorAdvice = "Muévete hacia la izquierda y \n hacia delante";
                }

                //Draw on the screen
                FormattedText floorAdviceText = new FormattedText(floorAdvice, CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight, new Typeface("Arial Black"), 22, Brushes.White);
                drawingContext.DrawText(floorAdviceText, new Point(90, 20));

                //Draw floor ellipse
                drawingContext.DrawEllipse(Brushes.Red, null, center_floor_2D, 50, 8);
            }

            // When the user is in a bad floor position, return -10
            return -10;
        }
        
        /// Código propio
        /// <summary>
        /// Draws an ellipse in the gesture goal
        /// </summary>
        /// <param name="goal">position of the hand</param>
        /// <param name="hand">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private bool DrawGesturePoint(Joint goal, Joint hand, DrawingContext drawingContext) {

            // New factor to give deep feeling being bigger or closer
            double ellipseSize = 2.0 / goal.Position.Z;

            // Transform world coordinates to screen coordinates
            DepthSpacePoint depth_goal = this.coordinateMapper.MapCameraPointToDepthSpace(goal.Position);
            Point goal_2D = new Point(depth_goal.X, depth_goal.Y);

            // If the hand is inside of the goal, return true
            if ((hand.Position.X < goal.Position.X + 0.05 && hand.Position.X > goal.Position.X - 0.05) &&
                (hand.Position.Y < goal.Position.Y + 0.05 && hand.Position.Y > goal.Position.Y - 0.05) &&
                (hand.Position.Z < goal.Position.Z + 0.05 && hand.Position.Z > goal.Position.Z - 0.05)) {

                drawingContext.DrawEllipse(this.gesturePointBrush, null, goal_2D, ellipseSize * HandSize, ellipseSize * HandSize);
                return true;
            }

            else {
                // If not, return false
                drawingContext.DrawEllipse(this.gesturePointBrush, null, goal_2D, ellipseSize * HandSize, ellipseSize * HandSize);
                return false;
            }
           
        }

        /// Código externo
        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext) {

            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }

        /// Código externo
        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e) {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }
       
    }
}
