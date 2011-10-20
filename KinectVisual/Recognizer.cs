using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Research.Kinect.Audio;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;

namespace Speech
{
    public class Recognizer
    {
        private const string RecognizerId = "SR_MS_en-US_Kinect_10.0";
        private bool kinectSpoken = false;
        private bool rerackSpoken = false;
        private KinectAudioSource kinectSource;
        private SpeechRecognitionEngine sre;

        public class SaidSomethingArgs : EventArgs
        {
            public string text;
        }

        public event EventHandler<SaidSomethingArgs> SaidSomething;

        Dictionary<string, string> rerackOptions = new Dictionary<string, string>()
        {
            {"line", "line"},
            {"triangle", "triangle"},
            {"dimond", "dimond"},
        };

        public Recognizer()
        {
            RecognizerInfo ri = SpeechRecognitionEngine.InstalledRecognizers().Where(r => r.Id == RecognizerId).FirstOrDefault();
            if (ri == null)
                return;

            sre = new SpeechRecognitionEngine(ri.Id);

            var kinect = new Choices();
            kinect.Add("kinect");
            var kinectGB = new GrammarBuilder();
            kinectGB.Append(kinect);

            var rerack = new Choices();
            rerack.Add("rerack");
            var rerackGB = new GrammarBuilder();
            rerackGB.Append(rerack);

            var options = new Choices();
            foreach (var phrase in rerackOptions)
                options.Add(phrase.Key);
            var optionsGB = new GrammarBuilder();
            optionsGB.Append(options);

            var allChoices = new Choices();
            allChoices.Add(kinectGB);
            allChoices.Add(rerackGB);
            allChoices.Add(optionsGB);

            var gb = new GrammarBuilder();
            gb.Culture = ri.Culture;
            gb.Append(allChoices);


            // Create the actual Grammar instance, and then load it into the speech recognizer.
            var g = new Grammar(gb);
            sre.LoadGrammar(g);
            sre.SpeechRecognized += SreSpeechRecognized;
            sre.SpeechHypothesized += SreSpeechHypothesized;
            sre.SpeechRecognitionRejected += SreSpeechRecognitionRejected;

            var t = new Thread(StartDMO);
            t.Start();
        }

        private void StartDMO()
        {
            kinectSource = new KinectAudioSource();
            kinectSource.SystemMode = SystemMode.OptibeamArrayOnly;
            kinectSource.FeatureMode = true;
            kinectSource.AutomaticGainControl = false;
            kinectSource.MicArrayMode = MicArrayMode.MicArrayAdaptiveBeam;
            var kinectStream = kinectSource.Start();
            sre.SetInputToAudioStream(kinectStream, new SpeechAudioFormatInfo(
                                                  EncodingFormat.Pcm, 16000, 16, 1,
                                                  32000, 2, null));
            sre.RecognizeAsync(RecognizeMode.Multiple);
        }

        void SreSpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Console.WriteLine("\nSpeech Rejected");
        }

        void SreSpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            Console.Write("\rSpeech Hypothesized: \t{0}", e.Result.Text, "\n");
        }

        void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            Console.WriteLine("\nSpeech Recognized: \t{0}", e.Result.Text);

            SaidSomethingArgs said = new SaidSomethingArgs();
            said.text = "";
            if (e.Result.Text.CompareTo("kinect") == 0)
                kinectSpoken = true;
            else if (!rerackSpoken && e.Result.Text.CompareTo("rerack") == 0)
                rerackSpoken = true;

            if (kinectSpoken)
                said.text = "kinect";
            if (rerackSpoken)
                said.text += " rerack ";

            if (kinectSpoken && rerackSpoken && rerackOptions.ContainsKey(e.Result.Text))
            {
                said.text += e.Result.Text;
                kinectSpoken = false;
                rerackSpoken = false;
            }
            SaidSomething(new object(), said);
        }

        public void Stop()
        {
            if (sre != null)
            {
                sre.RecognizeAsyncCancel();
                sre.RecognizeAsyncStop();
                kinectSource.Dispose();
            }
        }

    }
}
