#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The setup debug skip: calibrates every player from the current frame instead of a still hold. A player whose
    /// chest is out of view is dropped; when nobody is in view, everyone gets a centered origin so the game still starts.
    /// </summary>
    public static class SetupSkip
    {
        #region Public API

        /// <returns>The calibrated players, in player order.</returns>
        public static List<PlayerBody> CalibrateNow(IReadOnlyList<PlayerBody> bodies, bool storeRestJoints)
        {
            var calibrated = new List<PlayerBody>(bodies.Count);
            foreach (var body in bodies)
            {
                if (!body.IsCalibrated && body.TryGetChest(out var chest))
                {
                    body.SetOrigin(chest, body.TryGetKneeDrops(out var kneeDrops) ? kneeDrops : null, body.TrySampleShape(out var shape) ? shape : null);
                    if (storeRestJoints && body.TrySampleRestJoints(out var joints)) body.SetRestJoints(joints);
                }

                if (body.IsCalibrated) calibrated.Add(body);
            }

            if (calibrated.Count > 0) return calibrated;
            foreach (var body in bodies)
            {
                body.SetOrigin(Vector2.zero, null, null);
                calibrated.Add(body);
            }

            return calibrated;
        }

        #endregion
    }
}
