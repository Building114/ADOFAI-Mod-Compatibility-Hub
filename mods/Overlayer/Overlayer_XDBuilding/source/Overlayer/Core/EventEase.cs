using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overlayer.Core;

/// <summary>
/// Smooths changing targets without discontinuities when an animation is interrupted.
/// A state is shared by repeated uses of the same tag and animation parameters.
/// </summary>
public class EventEase {
    private sealed class AnimationState {
        public double From;
        public double Target;
        public double Displayed;
        public double Progress;
        public long StartedAt;
        public int LastFrame = -1;
        public bool Initialized;
    }

    private static readonly Dictionary<string, AnimationState> states = new(StringComparer.Ordinal);
    public Func<double> Getter { get; }
    public Ease Ease { get; set; }
    public double Speed { get; set; }
    public bool Invert { get; set; }
    public double Value => Getter();

    public EventEase(Func<double> getter, Ease ease = Ease.Linear, double speed = 500, bool invert = false) {
        Getter = getter;
        Ease = ease;
        Speed = speed;
        Invert = invert;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private double Fraction(long now, AnimationState state) {
        if(Speed <= 0 || !IsFinite(Speed)) return 1;
        double elapsed = Math.Max(0, now - state.StartedAt);
        if(elapsed >= Speed) return 1;
        float linear = (float)(elapsed / Speed);
        return DOVirtual.EasedValue(0f, 1f, linear, Ease);
    }

    private AnimationState Update(string id) {
        id ??= string.Empty;
        if(!states.TryGetValue(id, out var state)) {
            state = new AnimationState();
            states[id] = state;
        }

        // One sample per Unity frame: a large text layout can refer to the same
        // EasedValue hundreds of times without reading game data repeatedly.
        int frame = Time.frameCount;
        if(state.Initialized && state.LastFrame == frame) return state;

        double target = Getter?.Invoke() ?? 0;
        if(!IsFinite(target)) target = state.Initialized ? state.Target : 0;
        long now = FastDateTime.Now.Ticks / 10000;

        if(!state.Initialized) {
            // Never animate a freshly observed value from an artificial zero.
            state.From = state.Target = state.Displayed = target;
            state.StartedAt = now;
            state.Progress = 1;
            state.Initialized = true;
        } else {
            double progress = Fraction(now, state);
            double visible = progress >= 1
                ? state.Target
                : state.From + (state.Target - state.From) * progress;

            if(target != state.Target) {
                // Continue from what was actually visible, not the previous target.
                // This works for both rewinds and large forward jumps.
                state.From = visible;
                state.Target = target;
                state.StartedAt = now;
                state.Progress = 0;
            } else {
                state.Progress = progress;
            }
            state.Displayed = visible;
        }

        state.LastFrame = frame;
        // Prevent unbounded accumulation when scripts dynamically create tag IDs.
        if(states.Count > 2048) {
            // Almost all real profiles have fewer than a handful of IDs.
            // Evict only stale entries; do not reset the currently visible bar.
            var stale = new List<string>();
            foreach(var entry in states) {
                if(entry.Value.LastFrame < frame - 600) stale.Add(entry.Key);
            }
            foreach(string key in stale) states.Remove(key);
        }
        return state;
    }

    /// <summary>Current *visible* value, sampled once per frame.</summary>
    public double GetValue(string id) => Update(id).Displayed;

    // Keep the original public API available to external integrations.
    public double Compute(string id) {
        double fraction = Update(id).Progress;
        return Invert ? 1 - fraction : fraction;
    }

    public double GetPrevValue(string id) => Update(id).From;
}
