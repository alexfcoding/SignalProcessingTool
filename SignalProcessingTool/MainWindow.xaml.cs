﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;
using System.Diagnostics;
using DSPLib;
using NAudio;
using System.IO;

namespace SignalProcessingTool
{
    using System.Collections.Generic;
    using OxyPlot;
    using System.Diagnostics;
    using OxyPlot.Series;
    using OxyPlot.Axes;
    using System.Collections.ObjectModel;
    using System.Numerics;

    public partial class MainWindow : Window
    {
        int signalLength;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnCalcStart_Click(object sender, RoutedEventArgs e)
        {
            signalLength = 32768;
            SubstractWaveforms();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            signalLength = 44100;
            GenerateAcousticImpulse();
        }
        /// <summary>
        /// Read files, substract spectrum and inverse FFT.
        /// </summary>
        public void SubstractWaveforms()
        {

            #region
            // Read file with source signal
            double[] readFile = ReadWaveFile("recordings\\ObjectonTop7.wav"); 

            for (int i = 0; i < readFile.Length; i++)
            {
                readFile[i] = readFile[i] * 1f;
            }

            double[] fftValues = FFTMathNumerics(readFile).Item1;
            double[] fftFreq = FFTMathNumerics(readFile).Item2;
            Complex[] fftComplex = FFTMathNumerics(readFile).Item3;
            
            // Draw source signal spectrum
            Collection<PointClass> pointsSpectrum1 = new Collection<PointClass>();
            DrawSpectrum(pointsSpectrum1, fftValues, Plot1, fftFreq);

            // Read file with impact signal
            readFile = ReadWaveFile("recordings\\WithoutObjectOnTop7.wav"); 

            for (int i = 0; i < readFile.Length; i++)
            { 
                readFile[i] = readFile[i] * 1f;
            }

            double[] fftValues2 = FFTMathNumerics(readFile).Item1;
            Complex[] fftComplex2 = FFTMathNumerics(readFile).Item3;

            // Draw impact signal spectrum
            Collection<PointClass> pointsSpectrum2 = new Collection<PointClass>();
            DrawSpectrum(pointsSpectrum2, fftValues2, Plot2, fftFreq);

            // Read model signal
            readFile = ReadWaveFile("recordings\\CalculatedSignal.wav");

            for (int i = 0; i < readFile.Length; i++)
            {
                readFile[i] = readFile[i] * 1f;
            }

            Complex[] fftComplex3 = FFTMathNumerics(readFile).Item3;
            #endregion

            // Spectrum difference
            Complex[] complexDiff = SpectrumDivide(fftComplex, fftComplex2);
            double[] amps = AmpDiff(fftComplex, fftComplex2);
            // Inverse FFT and converting to short
            double[] diffSamples = InverseFFTMathNumerics(complexDiff);
            short[] outputSamples = diffSamples.Select(s => (short)s).ToArray();

            // Processing result
            WriteFile(outputSamples, "diff.wav");
            
            //Complex[] complexInput = new Complex[fftComplex3.Length];
            //for (int i = 0; i < complexInput.Length / 2; i++)
            //{
            //    Complex tmp = new Complex(fftComplex3[i].Real + amps[i], fftComplex3[i].Imaginary);
            //    complexInput[i] = tmp;
            //}

            StreamReader reader = new StreamReader("diffFunction.txt");

            var list = new List<double>();

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                double value = 0;

                if (!string.IsNullOrWhiteSpace(line) && double.TryParse(line, out value))
                    list.Add(value);
            }

            // Apply spectrum diff to signal
            // "+" for SpectrumDiff() and "*" for SpectrumDivide() function
            for (int i = 0; i < signalLength; i++)
            {
                fftComplex3[i] = fftComplex3[i] * complexDiff[i]; //+amps[i]; // Math.Abs(list[i]);
               // fftComplex3[fftComplex3.Length - i - 1] = fftComplex3[fftComplex3.Length - i - 1] * complexDiff[i];
            }

            // Spectrum difference amplitudes
            double[] diffSpectrum = new double[signalLength / 2];
            //diffSpectrum = ToLogScale(complexDiff, diffSpectrum);

            for (int i = 0; i < diffSpectrum.Length; i++)
                diffSpectrum[i] =(double)complexDiff[i].Magnitude;

            double[] finalDiffAmplitudes = new double[fftComplex3.Length];
            //finalDiffAmplitudes = ToLogScale(complexDiff, finalDiffAmplitudes);

            for (int i = 0; i < finalDiffAmplitudes.Length; i++)
                 finalDiffAmplitudes[i] = (double)fftComplex3[i].Magnitude;

            // Draw spectrum difference
            Collection<PointClass> pointsSpectrumDiff= new Collection<PointClass>();
            DrawSpectrum(pointsSpectrumDiff, diffSpectrum, Plot3, fftFreq);

            // Draw model spectrum with applied modification
            Collection<PointClass> pointsSpectrum3 = new Collection<PointClass>();
            DrawSpectrum(pointsSpectrum3, finalDiffAmplitudes, Plot4, fftFreq);
            
            // Inverse FFT and converting to short
            double[] diffFinalSamples = InverseFFTMathNumerics(fftComplex3);
            short[] outputFinalSamples = diffFinalSamples.Select(s => (short)s).ToArray();
            
            // Processing result
            
            WriteFile(outputFinalSamples, "HoleResult.wav"); 
        }

        /// <summary>
        /// Fill model with data and process
        /// </summary>
        public void GenerateAcousticImpulse()
        {
            FullAcousticModel Model1 = new FullAcousticModel();
            
            Model1.Pi = Math.PI;
            Model1.Diameter = Convert.ToDouble(pipeDiameterTextBox.Text);
            Model1.Thickness = 0.005;
            Model1.Density = 7800;
            Model1.PipeLength = 6;

            Model1.A1 = 0.2f;
            Model1.A2 = 100;
            Model1.A3 = 10000000;
            Model1.SignalDuration = 1f;
            Model1.Tc = 0.00004;
            Model1.Ti = 0.000022;
            Model1.Fd = 44100;
            Model1.ModNumber = 500;
            Model1.Cn = new double[500];
            Model1.Delta = new double[500];
            Model1.W = new double[500];
            Model1.Oi = new double[500];
            Model1.Sum = new double[44100];

            Model1.XCoordinate = Model1.PipeLength / 2;
            Model1.E = 200 * Math.Pow(10, 9);
            Model1.J = Model1.Pi * Math.Pow(Model1.Diameter, 3) * (double)Model1.Thickness / 8;
            Model1.Mass = Model1.Density * Model1.Pi * Model1.Diameter * Model1.Thickness;
            Model1.A4 = Model1.E * Model1.J / Model1.Mass;

            Model1.ComputeModel();

            double[] amplifiedSamples = Amplify(Model1.Sum);
            double[] fftValues = FFTMathNumerics(amplifiedSamples).Item1;
            double[] fftFreq = FFTMathNumerics(amplifiedSamples).Item2;
            Complex[] fftComplex = FFTMathNumerics(amplifiedSamples).Item3;

            Collection<PointClass> pointsModel = new Collection<PointClass>();
            short[] amplifiedSamplesShort = amplifiedSamples.Select(s => (short)s).ToArray();
            DrawImpulse(pointsModel, amplifiedSamplesShort, Plot1);

            Collection<PointClass> pointsSpectrum = new Collection<PointClass>();

            DrawSpectrum(pointsSpectrum, fftValues, Plot2, fftFreq);

            Complex[] equalizedSignal = ApplySpectrumFilter(fftComplex);
            double[] fftValuesEq = new double[32768];

            for (int i = 0; i < fftValuesEq.Length; i++)
            {
                fftValuesEq[i] = (double)equalizedSignal[i].Magnitude;
            }

            Collection<PointClass> pointsSpectrumEq = new Collection<PointClass>();

            DrawSpectrum(pointsSpectrumEq, fftValuesEq, Plot3, fftFreq);

            double[] equalizedSamples = InverseFFTMathNumerics(equalizedSignal);
            short[] outputSamples = equalizedSamples.Select(s => (short)s).ToArray();

            WriteFile(outputSamples, "ModelOutputStock.wav");
        }

        /// <summary>
        /// Simple wave loading and FFT.
        /// </summary>
        /// <param name="filePath"></param>
        void ProcessLoadedFile(string filePath)
        {
            double[] readFile = ReadWaveFile(filePath);

            for (int i = 0; i < readFile.Length; i++)
            {
                readFile[i] = readFile[i] * 0.7f;
            }

            double[] fftValues = FFTMathNumerics(readFile).Item1;
            double[] fftFreq = FFTMathNumerics(readFile).Item2;
            Complex[] fftComplex = FFTMathNumerics(readFile).Item3;
            Complex[] equalizedSignal = ApplySpectrumFilter(fftComplex);
            double[] equalizedSamples = InverseFFTMathNumerics(equalizedSignal);
            short[] outputSamples = equalizedSamples.Select(s => (short)s).ToArray();

            WriteFile(outputSamples, "InverseFFTSignal.wav");
        }

        /// <summary>
        /// Drawing function for waveforms.
        /// </summary>
        /// <param name="pointCollection">The collection of points.</param>
        /// <param name="valuesArray">The input array.</param>
        /// <param name="plotToDraw">The plot name.</param>
        void DrawImpulse(Collection<PointClass> pointCollection, short[] valuesArray, OxyPlot.Wpf.Plot plotToDraw)
        {
            plotToDraw.Series[0].ItemsSource = pointCollection;

            for (int i = 0; i < valuesArray.Length; i = i + 1)
            {
                    pointCollection.Add(
                    new PointClass
                    {
                        xPoint = i,
                        yPoint = valuesArray[i],
                    });
            }

            plotToDraw.InvalidatePlot(true);
        }

        /// <summary>
        /// Drawing function for waveforms spectrum.
        /// </summary>
        /// <param name="pointCollection">The collection of points.</param>
        /// <param name="valuesArray">The input array.</param>
        /// <param name="plotToDraw">The plot name.</param>
        void DrawSpectrum(Collection<PointClass> pointCollection, double[] valuesArray, OxyPlot.Wpf.Plot plotToDraw, double[] fftFreq = null)
        {
            plotToDraw.Series[0].ItemsSource = pointCollection;

            for (int i = 0; i < valuesArray.Length / 2; i = i + 1)
            {
                    pointCollection.Add(
                    new PointClass
                    {
                        xPoint = fftFreq[i],
                        yPoint = valuesArray[i],
                    });
            }

            plotToDraw.InvalidatePlot(true);
        }

        /// <summary>
        /// FFT using DSPLib library.
        /// </summary>
        /// <param name="inputArray">The input array.</param>
        /// <param name="logScale">if set to <c>true</c> [log scale]</param>
        /// <returns></returns>
        Tuple<double[],double[]> FastFourierDSPLib(double[] inputArray, bool logScale)
        {
            double[] tempArray = new double[2048];

            for (int i = 0; i < 2048; i++)
            {
                tempArray[i] = inputArray[i];
            }

            UInt32 length = 2048;
            double samplingRate = 44100;
            double[] spectrum = new double[4096];
            double[] wCoefs = DSP.Window.Coefficients(DSP.Window.Type.Hamming, length);
            double[] wInputData = DSP.Math.Multiply(tempArray, wCoefs);
            double wScaleFactor = DSP.Window.ScaleFactor.Signal(wCoefs);
            DSPLib.FFT fft = new DSPLib.FFT();
            fft.Initialize(length, length * 3);
            Complex[] cSpectrum = fft.Execute(wInputData);
            spectrum = DSP.ConvertComplex.ToMagnitude(cSpectrum);

            if (logScale == true)
            {
                spectrum = DSP.ConvertMagnitude.ToMagnitudeDBV(spectrum);

                for (int i = 0; i < spectrum.Length; i++)
                {
                    spectrum[i] -= 51;
                }
            }

            spectrum = DSP.Math.Multiply(spectrum, wScaleFactor);
            double[] freqSpan = fft.FrequencySpan(samplingRate);
            var tuple = new Tuple<double[], double[]>(spectrum, freqSpan);

            return tuple;
        }

        /// <summary>
        /// FFT using Math.Numerics library.
        /// </summary>
        /// <param name="inputArray">The input array.</param>
        /// <returns></returns>
        Tuple<double[], double[], Complex[]> FFTMathNumerics (double[] inputArray)
        {
            double[] tempArray = new double[signalLength];
            int fftBlockSize = signalLength;

            for (int i = 0; i < fftBlockSize; i++)
            {
                tempArray[i] = inputArray[i];
            }

            Complex[] complexInput = new Complex[tempArray.Length];

            for (int i = 0; i < complexInput.Length; i++)
            {
                Complex tmp = new Complex(tempArray[i], 0);
                complexInput[i] = tmp;
            }
                       
            MathNet.Numerics.IntegralTransforms.Fourier.Forward(complexInput);
            double[] outSamples = new double[complexInput.Length];

            for (int i = 0; i < outSamples.Length; i++)
            {
               //outSamples[i] = Math.Log10((double)complexInput[i].Magnitude) * 10;
               outSamples[i] = (double)complexInput[i].Magnitude;
            }  

            double[] freqSpan = MathNet.Numerics.IntegralTransforms.Fourier.FrequencyScale(signalLength, 44100);
            var tuple = new Tuple<double[], double[], Complex[]>(outSamples, freqSpan, complexInput);

            return tuple;
        }
        
        /// <summary>
        /// Inverse FFT using Math.Numerics library.
        /// </summary>
        /// <param name="inputArray">The input spectrum complex array.</param>
        /// <returns></returns>
        double[] InverseFFTMathNumerics (Complex[] inputSpectrum)
        {
            Complex[] ifftSpectrum = new Complex[inputSpectrum.Length];

            for (int i = 0; i < ifftSpectrum.Length; i++)
            {
                Complex tmp = new Complex(inputSpectrum[i].Real, inputSpectrum[i].Imaginary);
                ifftSpectrum[i] = tmp;
            }

            MathNet.Numerics.IntegralTransforms.Fourier.Inverse(ifftSpectrum);
            double[] outSamples = new double[ifftSpectrum.Length];

            for (int i = 0; i < outSamples.Length; i++)
            {
                outSamples[i] = (double)ifftSpectrum[i].Real;
            }

            return outSamples;
        }

        /// <summary>
        /// Applies the spectrum filter to signal.
        /// </summary>
        /// <param name="inputSpectrum">The input spectrum.</param>
        /// <returns></returns>
        Complex[] ApplySpectrumFilter(Complex[] inputSpectrum)
        {
            double frequency = 2000;
            double[] equalizeFunction = new double[signalLength / 2];

            for (int i = 0; i < equalizeFunction.Length; i++)
            {
                equalizeFunction[i] = 1 - (double) 1 / equalizeFunction.Length * i;

                if (equalizeFunction[i] < 0)
                    equalizeFunction[i] = 0;
            }

            double frequencyNumber = frequency / (44100f / signalLength);
            
            for (int i = 0; i < signalLength / 2; i++)
            {
                //if (i >= frequencyNumber)
                {
                    inputSpectrum[i] = inputSpectrum[i] * equalizeFunction[i];
                    inputSpectrum[inputSpectrum.Length - i - 1] = inputSpectrum[inputSpectrum.Length - i - 1] * equalizeFunction[i];
                }
            }

            return inputSpectrum;
        }

        /// <summary>
        /// Calculates the spectrum substraction.
        /// </summary>
        /// <param name="inputSpectrum">The input spectrum.</param>
        /// <param name="spectrumToSubstract">A spectrum to substract.</param>
        /// <returns></returns>
        Complex[] SpectrumDiff(Complex[] inputSpectrum, Complex[] spectrumToSubstract)
        {
            Complex[] complexOutput = new Complex[inputSpectrum.Length];

            for (int i = 0; i < complexOutput.Length; i++)
            {
                complexOutput[i] = inputSpectrum[i] - spectrumToSubstract[i];
            }

            return complexOutput;
        }

        double[] AmpDiff(Complex[] inputSpectrum, Complex[] spectrumToSubstract)
        {
            double[] amp = new double[inputSpectrum.Length];

            for (int i = 0; i < amp.Length; i++)
            {
                amp[i] = inputSpectrum[i].Magnitude - spectrumToSubstract[i].Magnitude;
            }

            return amp;
        }

        Complex[] SpectrumDivide(Complex[] inputSpectrum, Complex[] spectrumToSubstract)
        {
            Complex[] complexOutput = new Complex[inputSpectrum.Length];

            // Complex[] ampCoeff = new double[signalLength / 2];

            for (int i = 0; i < complexOutput.Length; i++)
            {
                complexOutput[i] = inputSpectrum[i] / spectrumToSubstract[i];
                //complexOutput[i] = 100;
            }

            return complexOutput;
        }

        /// <summary>
        /// Scales the specified input samples.
        /// </summary>
        /// <param name="inputSamples">The input samples.</param>
        /// <returns></returns>
        double[] Amplify(double[] inputSamples)
        {
            double max = -0.0000000000001f;

            for (int i = 0; i < inputSamples.Length; i++)
            {
                if (inputSamples[i] > max)
                    max = inputSamples[i];
            }

            for (int i = 0; i < inputSamples.Length; i++)
            {
                inputSamples[i] = (inputSamples[i] / max / 2 * 32768);
            }

            return inputSamples;
        }

        double[] ToLogScale(Complex[] inputArray, double[] outputArray)
        {
            for (int i = 0; i < inputArray.Length; i++)
            {
                outputArray[i] = Math.Log10((double)inputArray[i].Magnitude) * 10;
            }

            return outputArray;
        }

        /// <summary>
        /// Reads the wave file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        double[] ReadWaveFile(string filePath)
        {
            byte[] buffer = new byte[4];
            double[] dataStorage = new double[signalLength];
            long read = 0;
            long position = 0;

            NAudio.Wave.WaveFileReader waveReader = new NAudio.Wave.WaveFileReader(filePath);

            while (waveReader.Position < dataStorage.Length * 2)
            {
                read = waveReader.Read(buffer, 0, 4);

                for (int i = 0; i < read / 2; i += 1)
                {
                    dataStorage[position] = BitConverter.ToInt16(buffer, i * 2);
                    position++;
                }
            }

            return dataStorage;
        }
        
        /// <summary>
        /// Writes the wave file.
        /// </summary>
        /// <param name="inputArray">The input array.</param>
        void WriteFile (short[] inputArray, string filePath)
        {
            NAudio.Wave.WaveFormat waveFormat = new NAudio.Wave.WaveFormat(44100, 16, 1);
            NAudio.Wave.WaveFileWriter writer = new NAudio.Wave.WaveFileWriter(filePath, waveFormat);
            writer.WriteSamples(inputArray, 0, inputArray.Length);
            writer.Flush();
            writer.Dispose();
        }

        private void PipeDiameterTextBox_Copy2_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }

    public class PointClass
    {
        public double xPoint { get; set; }
        public double yPoint { get; set; }
        public double yPointMax { get; set; }
    }
}
