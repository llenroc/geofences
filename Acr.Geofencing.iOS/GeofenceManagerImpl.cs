using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CoreLocation;
using UIKit;


namespace Acr.Geofencing
{

    public class GeofenceManagerImpl : IGeofenceManager
    {
        readonly CLLocationManager locationManager;


        public GeofenceManagerImpl()
        {
            this.locationManager = new CLLocationManager();
            this.locationManager.RegionEntered += (sender, args) => this.DoBroadcast(args, GeofenceStatus.Entered);
            this.locationManager.RegionLeft += (sender, args) => this.DoBroadcast(args, GeofenceStatus.Exited);
        }


        public Distance DesiredAccuracy { get; set; } = Distance.FromKilometers(1);


        public async Task<GeofenceStatus> RequestState(GeofenceRegion region, CancellationToken? cancelToken)
        {
            var tcs = new TaskCompletionSource<GeofenceStatus>();
            cancelToken?.Register(() => tcs.TrySetCanceled());

            var handler = new EventHandler<CLRegionStateDeterminedEventArgs>((sender, args) =>
            {
                var clregion = args.Region as CLCircularRegion;
                if (clregion?.Identifier.Equals(region.Identifier) ?? false)
                {
                    var state = this.FromNative(args.State);
                    tcs.TrySetResult(state);
                }
            });

            try
            {
                this.locationManager.DidDetermineState += handler;
                var native = this.ToNative(region);
                this.locationManager.RequestState(native);
                return await tcs.Task;
            }
            finally
            {
                this.locationManager.DidDetermineState -= handler;
            }
        }


        public event EventHandler<GeofenceStatusChangedEventArgs> RegionStatusChanged;


        public IReadOnlyList<GeofenceRegion> MonitoredRegions
        {
            get
            {
                var list = this
                    .locationManager
                    .MonitoredRegions
                    .Select(x => x as CLCircularRegion)
                    .Where(x => x != null)
                    .Select(this.FromNative)
                    .ToList();
                return new ReadOnlyCollection<GeofenceRegion>(list);
            }
        }


        public void StartMonitoring(GeofenceRegion region)
        {
            //if !CLLocationManager.isMonitoringAvailableForClass(CLCircularRegion) {
            UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                var native = this.ToNative(region);
                this.locationManager.StartMonitoring(native);
            });
            //this.locationManager.DesiredAccuracy
            //this.locationManager.StartMonitoring(native, this.DesiredAccuracy.TotalMeters);
        }


        public void StopMonitoring(GeofenceRegion region)
        {
            var native = this.ToNative(region);
            this.locationManager.StopMonitoring(native);
        }


        public void StopAllMonitoring()
        {
            var natives = this
                .locationManager
                .MonitoredRegions
                .Select(x => x as CLCircularRegion)
                .Where(x => x != null)
                .ToList();

            foreach (var native in natives)
                this.locationManager.StopMonitoring(native);
        }


        protected virtual void DoBroadcast(CLRegionEventArgs args, GeofenceStatus status)
        {
            Debug.WriteLine("Firing geofence region event");
            var native = args.Region as CLCircularRegion;
            if (native == null)
                return;

            var region = this.FromNative(native);
            this.RegionStatusChanged?.Invoke(this, new GeofenceStatusChangedEventArgs(region, status));
        }


        protected virtual GeofenceRegion FromNative(CLCircularRegion native)
        {
            return new GeofenceRegion
            {
                Identifier = native.Identifier,
                Center = this.FromNative(native.Center),
                Radius = Distance.FromMeters(native.Radius)
            };
        }


        protected virtual GeofenceStatus FromNative(CLRegionState state)
        {
            switch (state)
            {
                case CLRegionState.Inside:
                    return GeofenceStatus.Entered;

                case CLRegionState.Outside:
                    return GeofenceStatus.Exited;

                case CLRegionState.Unknown:
                default:
                    return GeofenceStatus.Unknown;
            }
        }

        protected virtual Position FromNative(CLLocationCoordinate2D native)
        {
            return new Position(native.Latitude, native.Longitude);
        }


        protected virtual CLLocationCoordinate2D ToNative(Position position)
        {
            return new CLLocationCoordinate2D(position.Latitude, position.Longitude);
        }


        protected virtual CLCircularRegion ToNative(GeofenceRegion region)
        {
            return new CLCircularRegion(
                this.ToNative(region.Center),
                region.Radius.TotalMeters,
                region.Identifier
            )
            {
                NotifyOnExit = true,
                NotifyOnEntry = true
            };
        }
    }
}