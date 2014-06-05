using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Hardware;
using OxyPlot;
using OxyPlot.XamarinAndroid;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using OxyPlot.Series;

namespace XamarinRxDemo
{
    [Activity (Label = "XamarinRxDemo", MainLauncher = true)]
    public class MainActivity : Activity
    {
        int count = 1;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.Main);

            var plotView = FindViewById<PlotView> (Resource.Id.plotView);

            var sm = SensorManager.FromContext(this);
            var sensor = sm.GetDefaultSensor(SensorType.LinearAcceleration);

            var listener = new ObservableSensorListener (sm);
            sm.RegisterListener (listener, sensor, SensorDelay.Normal);

            // Here, we're going to take our raw data and make a version that 
            // is a bit less erratic, by averaging the last eight points. Trends
            // are easier to see in smoothed graphs than in raw data.
            var smoothedPoints = listener
                .Buffer(8, 1)
                .Select(xs => xs.Average());

            var normalPointsSeries = new LineSeries();
            var smoothedPointsSeries = new LineSeries();

            // We're going to plot data relative to the time the graph was started
            // (i.e. a point on the X-axis might be, '+3 seconds'), so we need to
            // remember the current time, and reset it every time we reset the graph.
            var currentTime = DateTimeOffset.Now;

            smoothedPoints
                .Select(x => new DataPoint((DateTimeOffset.Now - currentTime).TotalMilliseconds, x))
                .Subscribe(x => smoothedPointsSeries.Points.Add(x));

            listener
                .Select(x => new DataPoint((DateTimeOffset.Now - currentTime).TotalMilliseconds, x))
                .Subscribe(x => normalPointsSeries.Points.Add(x));

            var maxPlotLength = 10; // seconds 

            // Since LineSeries' backing field is just a List, we need to manually
            // tell OxyPlot to update the graph (we wouldn't want it updating for every
            // point anyways!). 
            // 
            // We also need to clear the graph from time to time, similar to how 
            // an Oscilloscope works. We'll do that here too
            Observable.Interval(TimeSpan.FromMilliseconds(250))
                .Subscribe(x => { 
                    // Observable.Interval schedules to background threads. We
                    // need to move to the UI thread to make changes
                    this.RunOnUiThread(() => {
                        // Clear the plot every maxPlotLength seconds
                        if (x % (maxPlotLength * 4) == 0) {
                            normalPointsSeries.Points.Clear();
                            smoothedPointsSeries.Points.Clear();
                            currentTime = DateTimeOffset.Now;
                        }

                        // Cause the Plot to redraw
                        plotView.InvalidatePlot(true);
                    });
                });

            // Set up our graph
            plotView.Model = new PlotModel();
            plotView.Model.Series.Add(normalPointsSeries);
            plotView.Model.Series.Add(smoothedPointsSeries);
        }
    }

    class ObservableSensorListener : Java.Lang.Object, ISensorEventListener, IObservable<float>, IDisposable
    {
        IDisposable inner;
        readonly Subject<float> sensorEvent = new Subject<float>();

        public ObservableSensorListener(SensorManager sm)
        {
            inner = Disposable.Create(() => sm.UnregisterListener (this));
        }

        public void OnAccuracyChanged(Sensor sensor, SensorStatus accuracy) { }

        public void OnSensorChanged (SensorEvent e)
        {
            // NB: e.Values[0] == X axis ("left / right" from the perspective
            // of looking at the phone)
            sensorEvent.OnNext(e.Values[0]);
        }

        public IDisposable Subscribe (IObserver<float> observer)
        {
            // You usually don't want to implement IObservable<T> directly, just
            // like you usually don't implement IEnumerable<T> directly, but instead
            // just use a List. 
            // 
            // In Rx, we usually use use Subjects to implement IObservable when we're
            // wrapping code that uses callbacks or events.
            return sensorEvent.Subscribe(observer);
        }

        public void Dispose ()
        {
            // Setting this to Disposable.Empty is an easy way to make sure that
            // nothing bad happens if someone double-disposes
            inner.Dispose();
            inner = Disposable.Empty;
        }
    }
}
