using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;


namespace SkeletalViewer
{
    class Cups
    {
        public List<Cup> cups;
        private const int CUP_SIZE = 20;
        public const int UP = -1;
        public const int DOWN = 1;

        public struct Cup
        {
            public Ellipse cup;

            public Cup(int xPos, int yPos, int width, int height)
            {
                cup = new Ellipse();
                cup.Height = height;
                cup.Width = width;
                cup.Stroke = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                cup.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Canvas.SetTop(cup, yPos);
                Canvas.SetLeft(cup, xPos);
            }
            
            public Cup(int xPos, int yPos, int width, int height, Brush brush)
            {
                cup = new Ellipse();
                cup.Height = height;
                cup.Width = width;
                cup.Stroke = brush;
                Canvas.SetTop(cup, yPos);
                Canvas.SetLeft(cup, xPos);
            }

            public void draw(UIElementCollection children)
            {
                children.Add(cup);
            }
        }

        public Cups()
        {
            cups = new List<Cup>();
        }

        public Cups(int numRows, int xPos, int yPos, int direction)
        {
            cups = new List<Cup>();
            for (int i = 0; i < numRows; i++)
                for (int j = 0; j < numRows - i; j++)
                    cups.Add(new Cup(xPos + j * CUP_SIZE + i * CUP_SIZE / 2, yPos + (int)((i * CUP_SIZE * Math.Sqrt(3)/2)* direction), CUP_SIZE, CUP_SIZE));
        }

        public void draw(UIElementCollection children)
        {
            foreach (Cup cup in cups)
                cup.draw(children);
        }
    }
}
