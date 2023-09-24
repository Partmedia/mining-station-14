using System.Linq;
using Content.Shared.Radiation.Components;
using Content.Server.Station;
using Content.Shared.Coordinates;
using Content.Shared.Station;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Maths;

namespace Content.Server.StationEvents.Events
{
    [UsedImplicitly]
    public sealed class RadiationStorm : StationEventSystem
    {
        // Based on Goonstation style radiation storm with some TG elements (announcer, etc.)

        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IRobustRandom _robustRandom = default!;

        public override string Prototype => "RadiationStorm";

        // Event specific details
        private float _timeUntilPulse;
        private const float MinPulseDelay = 0.3f;
        private const float MaxPulseDelay = 0.9f;

        private int _pulseCounter;
        private const int MinimumPulses = 180;
        private const int MaximumPulses = 540;

        private const int MinArea = 500;
        private Box2 _workingBounds;

        private const int MaxFail = 1080;
        private int _failCounter;

        private void ResetTimeUntilPulse()
        {
            _timeUntilPulse = _robustRandom.NextFloat() * (MaxPulseDelay - MinPulseDelay) + MinPulseDelay;
        }

        public override void Started()
        {
            ResetTimeUntilPulse();
            base.Started();
            var mod = Math.Sqrt(GetSeverityModifier());
            _pulseCounter = (int) (RobustRandom.Next(MinimumPulses, MaximumPulses) * mod);
        }

        public override void Ended()
        {
            base.Ended();
            _pulseCounter = 0;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!RuleStarted)
                return;

            if (_pulseCounter <= 0)
            {
                ForceEndSelf();
                return;
            }

            _timeUntilPulse -= frameTime;

            if (_timeUntilPulse <= 0.0f)
            {
                var mapId = GameTicker.DefaultMap;

                foreach (var grid in MapManager.GetAllMapGrids(mapId))
                {
                    if (!TryFindRandomGrid(grid, out var coordinates)) {
                        _failCounter++;
                        if (_failCounter > MaxFail)
                            ForceEndSelf();
                        return;
                    }
                        
                    _failCounter = 0;
                    _pulseCounter--;
                    SpawnPulse(coordinates);
                    ResetTimeUntilPulse();
                }   
            }
        }

        private void SpawnPulse(EntityCoordinates coordinates)
        {
            _entityManager.SpawnEntity("RadiationPulse", coordinates);
            ResetTimeUntilPulse();
        }

        private bool TryFindRandomGrid(MapGridComponent mapGrid, out EntityCoordinates coordinates)
        {
            var bounds = mapGrid.LocalAABB;
            if (Box2.Area(bounds) < MinArea)
            {
                if ((Box2.Area(_workingBounds) < MinArea))
                {
                    coordinates = default;
                    return false;
                }
                bounds = _workingBounds;
            } else
            {
                _workingBounds = bounds;
            }
            
            var randomX = _robustRandom.Next((int) bounds.Left, (int) bounds.Right);
            var randomY = _robustRandom.Next((int) bounds.Bottom, (int) bounds.Top);

            coordinates = mapGrid.ToCoordinates(randomX, randomY);

            // TODO: Need to get valid tiles? (maybe just move right if the tile we chose is invalid?)
            if (!coordinates.IsValid(_entityManager))
            {
                coordinates = default;
                return false;
            }
            return true;
        }
    }
}
