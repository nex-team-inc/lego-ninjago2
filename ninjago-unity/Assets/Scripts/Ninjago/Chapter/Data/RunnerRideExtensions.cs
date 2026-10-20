#nullable enable

using System.Collections.Generic;

namespace Nex.Ninjago
{
    public static class RunnerRideExtensions
    {
        static readonly VehicleType[] car = { VehicleType.Car };
        static readonly VehicleType[] skycraft = { VehicleType.Skycraft };
        static readonly VehicleType[] carThenSkycraft = { VehicleType.Car, VehicleType.Skycraft };

        public static IReadOnlyList<VehicleType> Vehicles(this RunnerRide ride) => ride switch
        {
            RunnerRide.Car => car,
            RunnerRide.Skycraft => skycraft,
            _ => carThenSkycraft,
        };

        public static VehicleType LastVehicle(this RunnerRide ride) => ride == RunnerRide.Car ? VehicleType.Car : VehicleType.Skycraft;

        public static bool Uses(this RunnerRide ride, VehicleType vehicle) => ride == RunnerRide.CarThenSkycraft || ride.LastVehicle() == vehicle;
    }
}
