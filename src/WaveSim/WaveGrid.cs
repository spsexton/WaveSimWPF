using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace WaveSim
{
    class WaveGrid
    {
        // Constants
        const int MinDimension = 5;
        const double Damping = 0.96;    // SAVE: 0.96
        const double SmoothingFactor = 2.0;     // Gives more weight to smoothing than to velocity

        // Private member data
        private Point3DCollection _ptBuffer1;
        private Point3DCollection _ptBuffer2;
        private Int32Collection _triangleIndices;
        private Random _rnd = new Random(48339);

        private int _dimension;

        // Pointers to which buffers contain:
        //    - Current: Most recent data
        //    - Old: Earlier data
        // These two pointers will swap, pointing to ptBuffer1/ptBuffer2 as we cycle the buffers
        private Point3DCollection _currBuffer;
        private Point3DCollection _oldBuffer;

        /// <summary>
        /// Construct new grid of a given dimension
        /// </summary>
        /// <param name="Dimension"></param>
        public WaveGrid(int Dimension)
        {
            if (Dimension < MinDimension)
                throw new ApplicationException(string.Format("Dimension must be at least {0}", MinDimension.ToString()));

            _ptBuffer1 = new Point3DCollection(Dimension * Dimension);
            _ptBuffer2 = new Point3DCollection(Dimension * Dimension);
            _triangleIndices = new Int32Collection((Dimension - 1) * (Dimension - 1) * 2);

            _dimension = Dimension;

            InitializePointsAndTriangles();

            _currBuffer = _ptBuffer2;
            _oldBuffer = _ptBuffer1;
        }

        /// <summary>
        /// Access to underlying grid data
        /// </summary>
        public Point3DCollection Points
        {
            get { return _currBuffer; }
        }

        /// <summary>
        /// Access to underlying triangle index collection
        /// </summary>
        public Int32Collection TriangleIndices
        {
            get { return _triangleIndices; }
        }

        /// <summary>
        /// Dimension of grid--same dimension for both X & Y
        /// </summary>
        public int Dimension
        {
            get { return _dimension; }
        }

        /// <summary>
        /// Induce new disturbance in grid at random location.  Height is 
        /// PeakValue +/- Delta.  (Random value in this range)
        /// </summary>
        /// <param name="BasePeakValue">Base height of new peak in grid</param>
        /// <param name="PlusOrMinus">Max amount to add/sub from BasePeakValue to get actual value</param>
        /// <param name="PeakWidth"># pixels wide, [1,4]</param>
        public void SetRandomPeak(double BasePeakValue, double Delta, int PeakWidth)
        {
            if ((PeakWidth < 1) || (PeakWidth > (_dimension / 2)))
                throw new ApplicationException("WaveGrid.SetRandomPeak: PeakWidth param must be <= half the dimension");

            int row = (int)(_rnd.NextDouble() * ((double)_dimension - 1.0));
            int col = (int)(_rnd.NextDouble() * ((double)_dimension - 1.0));

            // When caller specifies 0.0 peak, we assume always 0.0, so don't add delta
            if (BasePeakValue == 0.0)
                Delta = 0.0;

            double PeakValue = BasePeakValue + (_rnd.NextDouble() * 2 * Delta) - Delta;

            // row/col will be used for top-left corner.  But adjust, if that 
            // puts us out of the grid.
            if ((row + (PeakWidth - 1)) > (_dimension - 1))
                row = _dimension - PeakWidth;
            if ((col + (PeakWidth - 1)) > (_dimension - 1))
                col = _dimension - PeakWidth;

            // Change data 
            for (int ir = row; ir < (row + PeakWidth); ir++)
                for (int ic = col; ic < (col + PeakWidth); ic++)
                {
                    Point3D pt = _oldBuffer[(ir * _dimension) + ic];
                    pt.Y = pt.Y + (int)PeakValue;
                    _oldBuffer[(ir * _dimension) + ic] = pt;
                }
        }

        /// <summary>
        /// Induce wave along back edge of grid by creating large
        /// wall.
        /// </summary>
        /// <param name="WaveHeight"></param>
        public void InduceWave(double WaveHeight)
        {
            if (_dimension >= 15)
            {
                // Just set height of a few rows of points (in middle of grid)
                int NumRows = 20;
                //double[] SineCoeffs = new double[10] { 0.156, 0.309, 0.454, 0.588, 0.707, 0.809, 0.891, 0.951, 0.988, 1.0 };

                Point3D pt;
                int StartRow = _dimension / 2;
                for (int i = (StartRow - 1) * _dimension; i < (_dimension * (StartRow + NumRows)); i++)
                {
                    int RowNum = (i / _dimension) + StartRow;
                    pt = _oldBuffer[i];
                    //pt.Y = pt.Y + (WaveHeight * SineCoeffs[RowNum]);
                    pt.Y = pt.Y + WaveHeight ;
                    _oldBuffer[i] = pt;
                }
            }
        }

        /// <summary>
        /// Leave buffers in place, but change notation of which one is most recent
        /// </summary>
        private void SwapBuffers()
        {
            Point3DCollection temp = _currBuffer;
            _currBuffer = _oldBuffer;
            _oldBuffer = temp;
        }

        /// <summary>
        /// Clear out points/triangles and regenerates
        /// </summary>
        /// <param name="grid"></param>
        private void InitializePointsAndTriangles()
        {
            _ptBuffer1.Clear();
            _ptBuffer2.Clear();
            _triangleIndices.Clear();

            int nCurrIndex = 0;     // March through 1-D arrays

            for (int row = 0; row < _dimension; row++)
            {
                for (int col = 0; col < _dimension; col++)
                {
                    // In grid, X/Y values are just row/col numbers
                    _ptBuffer1.Add(new Point3D(col, 0.0, row));

                    // Completing new square, add 2 triangles
                    if ((row > 0) && (col > 0))
                    {
                        // Triangle 1
                        _triangleIndices.Add(nCurrIndex - _dimension - 1);
                        _triangleIndices.Add(nCurrIndex);
                        _triangleIndices.Add(nCurrIndex - _dimension);

                        // Triangle 2
                        _triangleIndices.Add(nCurrIndex - _dimension - 1);
                        _triangleIndices.Add(nCurrIndex - 1);
                        _triangleIndices.Add(nCurrIndex);
                    }

                    nCurrIndex++;
                }
            }

            // 2nd buffer exists only to have 2nd set of Z values
            _ptBuffer2 = _ptBuffer1.Clone();
        }

        /// <summary>
        /// Set height of all points in mesh to 0.0.  Also resets buffers to 
        /// original state.
        /// </summary>
        public void FlattenGrid()
        {
            Point3D pt;

            for (int i = 0; i < (_dimension * _dimension); i++)
            {
                pt = _ptBuffer1[i];
                pt.Y = 0.0;
                _ptBuffer1[i] = pt;
            }

            _ptBuffer2 = _ptBuffer1.Clone();
            _currBuffer = _ptBuffer2;
            _oldBuffer = _ptBuffer1;
        }

        /// <summary>
        /// Determine next state of entire grid, based on previous two states.
        /// This will have the effect of propagating ripples outward.
        /// </summary>
        public void ProcessWater()
        {
            // Note that we write into old buffer, which will then become our
            //    "current" buffer, and current will become old.  
            // I.e. What starts out in _currBuffer shifts into _oldBuffer and we 
            // write new data into _currBuffer.  But because we just swap pointers, 
            // we don't have to actually move data around.

            // When calculating data, we don't generate data for the cells around
            // the edge of the grid, because data smoothing looks at all adjacent
            // cells.  So instead of running [0,n-1], we run [1,n-2].

            double velocity;    // Rate of change from old to current
            double smoothed;    // Smoothed by adjacent cells
            double newHeight;
            int neighbors;

            int nPtIndex = 0;   // Index that marches through 1-D point array

            // Remember that Y value is the height (the value that we're animating)
            for (int row = 0; row < _dimension; row++)
            {
                for (int col = 0; col < _dimension; col++)
                {
                    velocity = -1.0 * _oldBuffer[nPtIndex].Y;     // row, col
                    smoothed = 0.0;

                    neighbors = 0;
                    if (row > 0)    // row-1, col
                    {
                        smoothed += _currBuffer[nPtIndex - _dimension].Y;
                        neighbors++;
                    }

                    if (row < (_dimension - 1))   // row+1, col
                    {
                        smoothed += _currBuffer[nPtIndex + _dimension].Y;
                        neighbors++;
                    }

                    if (col > 0)          // row, col-1
                    {
                        smoothed += _currBuffer[nPtIndex - 1].Y;
                        neighbors++;
                    }

                    if (col < (_dimension - 1))   // row, col+1
                    {
                        smoothed += _currBuffer[nPtIndex + 1].Y;
                        neighbors++;
                    }

                    // Will always have at least 2 neighbors
                    smoothed /= (double)neighbors;

                    // New height is combination of smoothing and velocity
                    newHeight = smoothed * SmoothingFactor + velocity;

                    // Damping
                    newHeight = newHeight * Damping;

                    // We write new data to old buffer
                    Point3D pt = _oldBuffer[nPtIndex];
                    pt.Y = newHeight;   // row, col
                    _oldBuffer[nPtIndex] = pt;

                    nPtIndex++;
                }
            }

            SwapBuffers();
        }
    }
}
