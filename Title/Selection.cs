using System;
using System.Collections.Generic;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Research.Kinect.Nui;

namespace ShapeGame
{
    class Selection
    {
        public static List<Selection> selections = new List<Selection>();

        Point center;
        string text;
        Brush brush;
        double fontSize;
        Label label;
        public Rect intersection;
        public bool isSelected = false;

        public Selection(string s, double size, Point ptCenter)
        {
            text = s;
            fontSize = size;
            center = ptCenter;
            label = null;
            brush = null;
        }

        public static void NewSelection(string s, double size, Point center)
        {
            selections.Add(new Selection(s, size, center));
        }

        void Advance()
        {
            if (brush == null)
                brush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            if (label == null)
                label = MakeSelection(text, new Rect(0, 0, 0, 0), brush);

            label.Foreground = brush;
            label.FontSize = fontSize;
            intersection = new Rect(label.RenderSize);
            intersection.X = center.X - intersection.Width / 2;
            intersection.Y = center.Y - intersection.Height / 2;
            label.SetValue(Canvas.LeftProperty, intersection.X);
            label.SetValue(Canvas.TopProperty, intersection.Y);

        }

        public static void Draw(UIElementCollection children)
        {
            foreach (var option in selections)
            {
                var rect = new Rectangle();
                rect.Width = option.intersection.Width;
                rect.Height = option.intersection.Height;
                rect.SetValue(Canvas.LeftProperty, option.intersection.X);
                rect.SetValue(Canvas.TopProperty, option.intersection.Y);
                rect.Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
                rect.StrokeThickness = 1;
                rect.Fill = new SolidColorBrush(Color.FromArgb(100, 255, 0, 0));
                children.Add(rect);
                option.Advance();
                children.Add(option.label);
            }
        }

        public static Label MakeSelection(string text, Rect bounds, Brush brush)
        {
            Label label = new Label();
            label.Content = text;
            if (bounds.Width != 0)
            {
                label.SetValue(Canvas.LeftProperty, bounds.Left);
                label.SetValue(Canvas.TopProperty, bounds.Top);
                label.Width = bounds.Width;
                label.Height = bounds.Height;
            }
            label.Foreground = brush;
            label.FontFamily = new FontFamily("Arial");
            label.FontWeight = FontWeight.FromOpenTypeWeight(600);
            label.FontStyle = FontStyles.Normal;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        public void changeColor(Brush color)
        {
            brush = color;
        }
    }
}
