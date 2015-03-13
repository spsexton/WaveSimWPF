using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WaveSim
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        private Vector3D zoomDelta;

        private WaveGrid _grid;
        private bool _rendering;
        private double _lastTimeRendered;
        private Random _rnd = new Random(1234);

        // Raindrop parameters.  Negative amplitude causes little tower of 
        // water to jump up vertically in the instant after the drop hits.
        private double _splashAmplitude; // Average height (depth, since negative) of raindrop splashes.  
        private double _splashDelta = 1.0;      // Actual splash height is Ampl +/- Delta (random)
        private double _raindropPeriodInMS;
        private double _waveHeight = 15.0;
        private int _dropSize;

        // Values to try:
        //   GridSize=20, RenderPeriod=125
        //   GridSize=50, RenderPeriod=50
        private const int GridSize = 250; //50;    
        private const double RenderPeriodInMS = 60; //50;    

        public Window1()
        {
            InitializeComponent();

            _splashAmplitude = -3.0;
            slidPeakHeight.Value = -1.0 * _splashAmplitude;

            _raindropPeriodInMS = 35.0;
            slidNumDrops.Value = 1.0 / (_raindropPeriodInMS / 1000.0);

            _dropSize = 1;
            slidDropSize.Value = _dropSize;

            // Set up the grid
            _grid = new WaveGrid(GridSize);
            meshMain.Positions = _grid.Points;
            meshMain.TriangleIndices = _grid.TriangleIndices;

            // On each WheelMouse change, we zoom in/out a particular % of the original distance
            const double ZoomPctEachWheelChange = 0.02;
            zoomDelta = Vector3D.Multiply(ZoomPctEachWheelChange, camMain.LookDirection);
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                // Zoom in
                camMain.Position = Point3D.Add(camMain.Position, zoomDelta);
            else
                // Zoom out
                camMain.Position = Point3D.Subtract(camMain.Position, zoomDelta);
        }

        // Start/stop animation
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!_rendering)
            {
                //_grid = new WaveGrid(GridSize);        // New grid allows buffer reset
                _grid.FlattenGrid();
                meshMain.Positions = _grid.Points;

                _lastTimeRendered = 0.0;
                CompositionTarget.Rendering += new EventHandler(CompositionTarget_Rendering);
                btnStart.Content = "Stop";
                _rendering = true;
            }
            else
            {
                CompositionTarget.Rendering -= new EventHandler(CompositionTarget_Rendering);
                btnStart.Content = "Start";
                _rendering = false;
            }
        }

        void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            RenderingEventArgs rargs = (RenderingEventArgs)e;
            if ((rargs.RenderingTime.TotalMilliseconds - _lastTimeRendered) > RenderPeriodInMS)
            {
                // Unhook Positions collection from our mesh, for performance
                // (see http://blogs.msdn.com/timothyc/archive/2006/08/31/734308.aspx)
                meshMain.Positions = null;

                // Do the next iteration on the water grid, propagating waves
                double NumDropsThisTime = RenderPeriodInMS / _raindropPeriodInMS;
                
                // Result at this point for number of drops is something like
                // 2.25.  We'll induce integer portion (e.g. 2 drops), then
                // 25% chance for 3rd drop.
                int NumDrops = (int)NumDropsThisTime;   // trunc
                for (int i = 0; i < NumDrops; i++)
                    _grid.SetRandomPeak(_splashAmplitude, _splashDelta, _dropSize);

                if ((NumDropsThisTime - NumDrops) > 0)
                {
                    double DropChance = NumDropsThisTime - NumDrops;
                    if (_rnd.NextDouble() <= DropChance)
                        _grid.SetRandomPeak(_splashAmplitude, _splashDelta, _dropSize);
                }

                _grid.ProcessWater();

                // Then update our mesh to use new Z values
                meshMain.Positions = _grid.Points;

                _lastTimeRendered = rargs.RenderingTime.TotalMilliseconds;
            }
        }

        private void slidPeakHeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Slider runs [0,30], so our amplitude runs [-30,0].  
            // Negative amplitude is desirable because we see little towers of 
            // water as each drop bloops in.
            _splashAmplitude = -1.0 * slidPeakHeight.Value;
        }

        private void slidNumDrops_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Slider runs from [1,1000], with 1000 representing more drops (1 every ms) and 
            // 1 representing fewer (1 ever 1000 ms).  This is to make slider seem natural
            // to user.  But we need to invert it, to get actual period (ms)
            _raindropPeriodInMS = (1.0 / slidNumDrops.Value) * 1000.0;
        }

        private void btnWave_Click(object sender, RoutedEventArgs e)
        {
            _grid.InduceWave(_waveHeight);
        }

        private void slidDropSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _dropSize = (int)slidDropSize.Value;
        }
    }
}
