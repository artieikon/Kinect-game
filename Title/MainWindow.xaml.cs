/////////////////////////////////////////////////////////////////////////
//
// This module contains code to do Kinect NUI initialization,
// processing, displaying players on screen, and sending updated player
// positions to the game portion for hit testing.
//
// Copyright © Microsoft Corporation.  All rights reserved.  
// This code is licensed under the terms of the 
// Microsoft Kinect for Windows SDK (Beta) from Microsoft Research 
// License Agreement: http://research.microsoft.com/KinectSDK-ToU
//
/////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using System.Media;
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
using Microsoft.Research.Kinect.Nui;
using ShapeGame_Utils;


// Since the timer resolution defaults to about 10ms precisely, we need to
// increase the resolution to get framerates above between 50fps with any
// consistency.
using System.Runtime.InteropServices;
public class Win32
{
    [DllImport("Winmm.dll")]
    public static extern int timeBeginPeriod(UInt32 uPeriod);
}


namespace ShapeGame
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        const int TimerResolution = 2;  // ms
        const int NumIntraFrames = 3;
        const int MaxShapes = 80;
        const double MaxFramerate = 70;
        const double MinFramerate = 15;
        const double MinShapeSize = 12;
        const double MaxShapeSize = 90;
        const double DefaultDropRate = 2.5;
        const double DefaultDropSize = 32.0;
        const double DefaultDropGravity = 1.0;

        public MainWindow()
        {
            InitializeComponent();
            // Restore window state to that last used
            Rect bounds = Properties.Settings.Default.PrevWinPosition;
            if (bounds.Right != bounds.Left)
            {
                this.Top = bounds.Top;
                this.Left = bounds.Left;
                this.Height = bounds.Height;
                this.Width = bounds.Width;
            }
            this.WindowState = (WindowState)Properties.Settings.Default.WindowState;
        }

        public class Player
        {
            public Rect leftHand;
            public Rect rightHand;
            public bool isAlive;
            public DateTime lastUpdated;
            private Brush brJoints;
            private Brush brBones;
            private Rect playerBounds;
            private Point playerCenter;
            private double playerScale;
            private int id;
            private static int colorId = 0;

            private const double BONE_SIZE = 0.01;
            private const double HEAD_SIZE = 0.075;
            private const double HAND_SIZE = 0.03;

            // Keeping track of all bone segments of interest as well as head, hands and feet
            public Dictionary<Bone, BoneData> segments = new Dictionary<Bone, BoneData>();

            public Player(int SkeletonSlot)
            {
                id = SkeletonSlot;

                // Generate one of 7 colors for player
                int[] iMixr = { 1, 1, 1, 0, 1, 0, 0 };
                int[] iMixg = { 1, 1, 0, 1, 0, 1, 0 };
                int[] iMixb = { 1, 0, 1, 1, 0, 0, 1 };
                byte[] iJointCols = { 245, 200 };
                byte[] iBoneCols = { 235, 160 };

                int i = colorId;
                colorId = (colorId + 1) % iMixr.Count();

                brJoints = new SolidColorBrush(Color.FromRgb(iJointCols[iMixr[i]], iJointCols[iMixg[i]], iJointCols[iMixb[i]]));
                brBones = new SolidColorBrush(Color.FromRgb(iBoneCols[iMixr[i]], iBoneCols[iMixg[i]], iBoneCols[iMixb[i]]));
                lastUpdated = DateTime.Now;
            }

            public int getId()
            {
                return id;
            }

            public void setBounds(Rect r)
            {
                playerBounds = r;
                playerCenter.X = (playerBounds.Left + playerBounds.Right) / 2;
                playerCenter.Y = (playerBounds.Top + playerBounds.Bottom) / 2;
                playerScale = Math.Min(playerBounds.Width, playerBounds.Height / 2);
            }

            void UpdateSegmentPosition(JointID j1, JointID j2, Segment seg)
            {
                var bone = new Bone(j1, j2);
                if (segments.ContainsKey(bone))
                {
                    BoneData data = segments[bone];
                    data.UpdateSegment(seg);
                    segments[bone] = data;
                }
                else
                    segments.Add(bone, new BoneData(seg));
            }

            public void UpdateBonePosition(Microsoft.Research.Kinect.Nui.JointsCollection joints, JointID j1, JointID j2)
            {
                var seg = new Segment(joints[j1].Position.X * playerScale + playerCenter.X,
                                      playerCenter.Y - joints[j1].Position.Y * playerScale,
                                      joints[j2].Position.X * playerScale + playerCenter.X,
                                      playerCenter.Y - joints[j2].Position.Y * playerScale);
                seg.radius = Math.Max(3.0, playerBounds.Height * BONE_SIZE) / 2;
                UpdateSegmentPosition(j1, j2, seg);
            }

            public void UpdateJointPosition(Microsoft.Research.Kinect.Nui.JointsCollection joints, JointID j)
            {
                var seg = new Segment(joints[j].Position.X * playerScale + playerCenter.X,
                                      playerCenter.Y - joints[j].Position.Y * playerScale);
                seg.radius = playerBounds.Height * ((j == JointID.Head) ? HEAD_SIZE : HAND_SIZE) / 2;
                UpdateSegmentPosition(j, j, seg);
                if (j == JointID.HandLeft) {
                    leftHand = new Rect(new Point(joints[j].Position.X * playerScale + playerCenter.X,
                                      playerCenter.Y - joints[j].Position.Y * playerScale), new Size(seg.radius, seg.radius));
                    foreach (Selection selection in Selection.selections)
                    {
                        if (!selection.isSelected && leftHand.IntersectsWith(selection.intersection))
                        {
                            selection.changeColor(new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)));
                            selection.isSelected = true;
                        }
                        else if (selection.isSelected && !leftHand.IntersectsWith(selection.intersection))
                        {
                            selection.changeColor(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)));
                            selection.isSelected = false;
                        }
                    }
                }
                if (j == JointID.HandRight)
                    rightHand = new Rect(new Point(joints[j].Position.X * playerScale + playerCenter.X,
                                      playerCenter.Y - joints[j].Position.Y * playerScale), new Size(seg.radius, seg.radius));
            }

            public void Draw(UIElementCollection children)
            {
                if (!isAlive)
                    return;

                // Draw all bones first, then circles (head and hands).
                DateTime cur = DateTime.Now;
                foreach (var segment in segments)
                {
                    Segment seg = segment.Value.GetEstimatedSegment(cur);
                    if (!seg.IsCircle())
                    {
                        var line = new Line();
                        line.StrokeThickness = seg.radius * 2;
                        line.X1 = seg.x1;
                        line.Y1 = seg.y1;
                        line.X2 = seg.x2;
                        line.Y2 = seg.y2;
                        line.Stroke = brBones;
                        line.StrokeEndLineCap = PenLineCap.Round;
                        line.StrokeStartLineCap = PenLineCap.Round;
                        children.Add(line);
                    }
                }
                foreach (var segment in segments)
                {
                    Segment seg = segment.Value.GetEstimatedSegment(cur); 
                    if (seg.IsCircle())
                    {
                        var circle = new Ellipse();
                        circle.Width = seg.radius * 2;
                        circle.Height = seg.radius * 2;
                        circle.SetValue(Canvas.LeftProperty, seg.x1 - seg.radius);
                        circle.SetValue(Canvas.TopProperty, seg.y1 - seg.radius);
                        circle.Stroke = brJoints;
                        circle.StrokeThickness = 1;
                        circle.Fill = brBones;
                        children.Add(circle);
                    }
                }

                if (!leftHand.IsEmpty)
                {
                    var circle = new Ellipse();
                    circle.Width = leftHand.Width * 2;
                    circle.Height = leftHand.Width * 2;
                    circle.SetValue(Canvas.LeftProperty, leftHand.X - leftHand.Width);
                    circle.SetValue(Canvas.TopProperty, leftHand.Y - leftHand.Width);
                    circle.Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
                    circle.StrokeThickness = 1;
                    circle.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
                    children.Add(circle);
                }

                // Remove unused players after 1/2 second.
                if (DateTime.Now.Subtract(lastUpdated).TotalMilliseconds > 500)
                    isAlive = false;
            }
        }

        public Dictionary<int, Player> players = new Dictionary<int, Player>();

        DateTime lastFrameDrawn = DateTime.MinValue;
        DateTime predNextFrame = DateTime.MinValue;
        double actualFrameTime = 0;

        // Player(s) placement in scene (z collapsed):
        Rect playerBounds;
        Rect screenRect;
        
        double targetFramerate = MaxFramerate;
        int frameCount = 0;
        bool runningGameThread = false;
        bool nuiInitialized = false;
        int playersAlive = 0;

        Runtime nui = new Runtime();

        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame skeletonFrame = e.SkeletonFrame;

            int iSkeletonSlot = 0;

            foreach (SkeletonData data in skeletonFrame.Skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    Player player;
                    if (players.ContainsKey(iSkeletonSlot))
                    {
                        player = players[iSkeletonSlot];
                    }
                    else
                    {
                        player = new Player(iSkeletonSlot);
                        player.setBounds(playerBounds);
                        players.Add(iSkeletonSlot, player);
                    }

                    player.lastUpdated = DateTime.Now;

                    // Update player's bone and joint positions
                    if (data.Joints.Count > 0)
                    {
                        player.isAlive = true;

                        // Head, hands, feet (hit testing happens in order here)
                        player.UpdateJointPosition(data.Joints, JointID.Head);
                        player.UpdateJointPosition(data.Joints, JointID.HandLeft);
                        player.UpdateJointPosition(data.Joints, JointID.HandRight);
                        player.UpdateJointPosition(data.Joints, JointID.FootLeft);
                        player.UpdateJointPosition(data.Joints, JointID.FootRight);

                        // Hands and arms
                        player.UpdateBonePosition(data.Joints, JointID.HandRight, JointID.WristRight);
                        player.UpdateBonePosition(data.Joints, JointID.WristRight, JointID.ElbowRight);
                        player.UpdateBonePosition(data.Joints, JointID.ElbowRight, JointID.ShoulderRight);

                        player.UpdateBonePosition(data.Joints, JointID.HandLeft, JointID.WristLeft);
                        player.UpdateBonePosition(data.Joints, JointID.WristLeft, JointID.ElbowLeft);
                        player.UpdateBonePosition(data.Joints, JointID.ElbowLeft, JointID.ShoulderLeft);
                        
                        // Head and Shoulders
                        player.UpdateBonePosition(data.Joints, JointID.ShoulderCenter, JointID.Head);
                        player.UpdateBonePosition(data.Joints, JointID.ShoulderLeft, JointID.ShoulderCenter);
                        player.UpdateBonePosition(data.Joints, JointID.ShoulderCenter, JointID.ShoulderRight);

                        // Legs
                        player.UpdateBonePosition(data.Joints, JointID.HipLeft, JointID.KneeLeft);
                        player.UpdateBonePosition(data.Joints, JointID.KneeLeft, JointID.AnkleLeft);
                        player.UpdateBonePosition(data.Joints, JointID.AnkleLeft, JointID.FootLeft);

                        player.UpdateBonePosition(data.Joints, JointID.HipRight, JointID.KneeRight);
                        player.UpdateBonePosition(data.Joints, JointID.KneeRight, JointID.AnkleRight);
                        player.UpdateBonePosition(data.Joints, JointID.AnkleRight, JointID.FootRight);

                        player.UpdateBonePosition(data.Joints, JointID.HipLeft, JointID.HipCenter);
                        player.UpdateBonePosition(data.Joints, JointID.HipCenter, JointID.HipRight);

                        // Spine
                        player.UpdateBonePosition(data.Joints, JointID.HipCenter, JointID.ShoulderCenter);
                    }
                }
                iSkeletonSlot++;
            }
        }

        void CheckPlayers()
        {
            foreach (var player in players)
            {
                if (!player.Value.isAlive)
                {
                    // Player left scene since we aren't tracking it anymore, so remove from dictionary
                    players.Remove(player.Value.getId());
                    break;
                }
            }

            // Count alive players
            int alive = 0;
            foreach (var player in players)
            {
                if (player.Value.isAlive)
                    alive++;
            }
            if (alive != playersAlive)
            {
                if ((playersAlive == 0))
                    BannerText.NewBanner(Properties.Resources.Vocabulary, screenRect, true, Color.FromArgb(200, 255, 255, 255));

                playersAlive = alive;
            }
        }

        void nui_ColorFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            // 32-bit per pixel, RGBA image
            PlanarImage Image = e.ImageFrame.Image;
            video.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, Image.Bits, Image.Width * Image.BytesPerPixel);
        }
      
        private bool InitializeNui()
        {
            UninitializeNui();
            if (nui == null)
                return false;
            try
            {
                nui.Initialize(RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking | RuntimeOptions.UseColor);
            }
            catch (Exception _Exception)
            {
                Console.WriteLine(_Exception.ToString());
                return false;
            }

            nui.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);
            nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
            nui.SkeletonEngine.TransformSmooth = true;
            nuiInitialized = true;
            return true;
        }

        private void UninitializeNui()
        {
            if ((nui != null) && (nuiInitialized))
                nui.Uninitialize();
            nuiInitialized = false;
        }

        private void Playfield_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePlayfieldSize();
        }

        private void UpdatePlayfieldSize()
        {
            // Size of player wrt size of playfield, putting ourselves low on the screen.
            screenRect.X = 0;
            screenRect.Y = 0;
            screenRect.Width = playfield.ActualWidth;
            screenRect.Height = playfield.ActualHeight;

            BannerText.UpdateBounds(screenRect);

            playerBounds.X = 0;
            playerBounds.Width = playfield.ActualWidth;
            playerBounds.Y = playfield.ActualHeight * 0.2;
            playerBounds.Height = playfield.ActualHeight * 0.75;

            foreach (var player in players)
                player.Value.setBounds(playerBounds);

            Rect rFallingBounds = playerBounds;
            rFallingBounds.Y = 0;
            rFallingBounds.Height = playfield.ActualHeight;
        }
        
        private void Window_Loaded(object sender, EventArgs e)
        {
            playfield.ClipToBounds = true;

            UpdatePlayfieldSize();

            if ((nui != null) && InitializeNui())
            {
                nui.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_ColorFrameReady);
                nui.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);
            }
            else
            {
                BannerText.NewBanner(Properties.Resources.NoKinectError, screenRect, false, Color.FromArgb(90, 255, 255, 255));
            }

            Win32.timeBeginPeriod(TimerResolution);
            var gameThread = new Thread(GameThread);
            gameThread.SetApartmentState(ApartmentState.STA);
            gameThread.Start();
        }

        private void GameThread()
        {
            Selection.NewSelection("test", 30, new Point(200, 450));
            runningGameThread = true;
            predNextFrame = DateTime.Now;
            actualFrameTime = 1000.0 / targetFramerate;

            // Try to dispatch at as constant of a framerate as possible by sleeping just enough since
            // the last time we dispatched.
            while (runningGameThread)
            {
                // Calculate average framerate.  
                DateTime now = DateTime.Now;
                if (lastFrameDrawn == DateTime.MinValue)
                    lastFrameDrawn = now;
                double ms = now.Subtract(lastFrameDrawn).TotalMilliseconds;
                actualFrameTime = actualFrameTime * 0.95 + 0.05 * ms;
                lastFrameDrawn = now;
                
                // Adjust target framerate down if we're not achieving that rate
                frameCount++;
                if (((frameCount % 100) == 0) && (1000.0 / actualFrameTime < targetFramerate * 0.92))
                    targetFramerate = Math.Max(MinFramerate, (targetFramerate + 1000.0 / actualFrameTime) / 2);

                if (now > predNextFrame)
                    predNextFrame = now;
                else
                {
                    double msSleep = predNextFrame.Subtract(now).TotalMilliseconds;
                    if (msSleep >= TimerResolution)  
                        Thread.Sleep((int)(msSleep + 0.5));
                }
                predNextFrame += TimeSpan.FromMilliseconds(1000.0 / targetFramerate);

                Dispatcher.Invoke(DispatcherPriority.Send,
                    new Action<int>(HandleGameTimer), 0);
            }
        }

        private void HandleGameTimer(int param)
        {

            // Draw new Wpf scene by adding all objects to canvas
            playfield.Children.Clear();
            Selection.Draw(playfield.Children);
            foreach (var player in players)
                player.Value.Draw(playfield.Children);
            BannerText.Draw(playfield.Children);
            FlyingText.Draw(playfield.Children);


            CheckPlayers();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            runningGameThread = false;
            Properties.Settings.Default.PrevWinPosition = this.RestoreBounds;
            Properties.Settings.Default.WindowState = (int)this.WindowState;
            Properties.Settings.Default.Save();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            UninitializeNui();
            Environment.Exit(0);
        }
    }
}
