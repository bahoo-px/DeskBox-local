using System.Numerics;
using DeskBox.Services;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;

namespace DeskBox.Views;

/// <summary>
/// The left stage is one persistent illustration canvas. Each step owns a
/// single hero element (logo mark / drop box / keycap / tray / feature tiles)
/// that swaps via a soft crossfade + settle — no lateral page motion. Per-step
/// loops (file flight, keycap press, tray ping, tile float) are fully stopped
/// when leaving the step.
/// </summary>
public sealed partial class OnboardingWindow
{
    private const int SceneTransitionMs = 380;

    private void ApplySceneState(int step, bool animate)
    {
        StopSceneLoops();

        // A shared halo is the only element that persists across every step.
        Set(SceneHalo, 0, 0, step == 0 ? 1f : 0.92f, step == 0 ? 0.55f : 0.4f, animate);

        Set(SceneMark, 0, 0, step == 0 ? 1f : 0.9f, step == 0 ? 1f : 0f, animate);

        bool drop = step == 1;
        Set(SceneDropBox, 0, 0, drop ? 1f : 0.94f, drop ? 1f : 0f, animate);
        Set(SceneFileToken, 0, 0, 1f, drop ? 1f : 0f, animate, 260);
        Set(SceneDropBoxHalo, 0, 0, 1f, 0f, animate, 220);
        Set(SceneMoveBadge, 0, 0, 1f, 0f, animate, 220);

        bool keycap = step == 2;
        Set(SceneKeycapHost, 0, keycap ? 0f : 12f, keycap ? 1f : 0.9f, keycap ? 1f : 0f, animate);
        Set(SceneKeycapHalo, 0, 0, 1f, 0f, animate, 220);

        bool tray = step == 3;
        Set(SceneTaskbar, 0, tray ? 0f : 10f, tray ? 1f : 0.95f, tray ? 1f : 0f, animate);
        Set(SceneTrayMenu, 0, tray ? 0f : 10f, tray ? 1f : 0.9f, tray ? 1f : 0f, animate);
        Set(SceneTrayHalo, 0, 0, 1f, 0f, animate, 220);

        bool features = step == 4;
        Set(SceneFeatureTiles, 0, features ? 0f : 14f, features ? 1f : 0.9f, features ? 1f : 0f, animate);
    }

    private void Set(
        UIElement element,
        float x,
        float y,
        float scale,
        float opacity,
        bool animate,
        int durationMs = SceneTransitionMs)
    {
        var visual = GetVisual(element);
        visual.StopAnimation("Translation");
        visual.StopAnimation("Scale");
        visual.StopAnimation("Opacity");
        var targetTranslation = new Vector3(x, y, 0);
        var targetScale = new Vector3(scale, scale, 1f);

        if (!animate || !WindowsCompatibilityService.AreAnimationsEnabled)
        {
            element.Translation = targetTranslation;
            visual.Scale = targetScale;
            visual.Opacity = opacity;
            return;
        }

        var compositor = visual.Compositor;
        var ease = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));
        var duration = TimeSpan.FromMilliseconds(durationMs);

        var translation = compositor.CreateVector3KeyFrameAnimation();
        translation.Duration = duration;
        translation.InsertKeyFrame(0f, element.Translation);
        translation.InsertKeyFrame(1f, targetTranslation, ease);
        visual.StartAnimation("Translation", translation);

        var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Duration = duration;
        scaleAnimation.InsertKeyFrame(0f, visual.Scale);
        scaleAnimation.InsertKeyFrame(1f, targetScale, ease);
        visual.StartAnimation("Scale", scaleAnimation);

        var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(
            Math.Min(durationMs, SceneTransitionMs - 80));
        opacityAnimation.InsertKeyFrame(0f, visual.Opacity);
        opacityAnimation.InsertKeyFrame(1f, opacity, ease);
        visual.StartAnimation("Opacity", opacityAnimation);
    }

    private void StartSceneLoops(int step)
    {
        if (!WindowsCompatibilityService.AreAnimationsEnabled)
        {
            return;
        }

        switch (step)
        {
            case 0:
                StartMarkFloatLoop();
                break;
            case 1:
                StartFileFlightLoop();
                break;
            case 2:
                StartKeycapPressLoop();
                break;
            case 3:
                StartTrayHaloLoop();
                break;
            case 4:
                StartFeatureFloatLoop();
                break;
        }
    }

    private void StopSceneLoops()
    {
        StopLoopAnimation(SceneHalo);
        StopLoopAnimation(SceneMark);
        StopLoopAnimation(SceneDropBox);
        StopLoopAnimation(SceneDropBoxHalo);
        StopLoopAnimation(SceneFileToken);
        StopLoopAnimation(SceneMoveBadge);
        StopLoopAnimation(SceneKeycap);
        StopLoopAnimation(SceneKeycapHalo);
        StopLoopAnimation(SceneTrayHalo);
        foreach (var child in SceneFeatureTiles.Children)
        {
            StopLoopAnimation(child);
        }
    }

    private static void StopLoopAnimation(UIElement element)
    {
        var visual = GetVisual(element);
        visual.StopAnimation("Translation");
        visual.StopAnimation("Scale");
        visual.StopAnimation("Opacity");
    }

    // ── Step 1: the mark breathes gently over the halo ──

    private void StartMarkFloatLoop()
    {
        var markVisual = GetVisual(SceneMark);
        var drift = markVisual.Compositor.CreateVector3KeyFrameAnimation();
        drift.Duration = TimeSpan.FromMilliseconds(3400);
        drift.IterationBehavior = AnimationIterationBehavior.Forever;
        drift.InsertKeyFrame(0f, Vector3.Zero);
        drift.InsertKeyFrame(0.5f, new Vector3(0, -5f, 0), EaseOut());
        drift.InsertKeyFrame(1f, Vector3.Zero, EaseOut());
        markVisual.StartAnimation("Translation", drift);
    }

    // ── Step 2: a file arcs into the box, badge pops on landing ──

    private void StartFileFlightLoop()
    {
        const float flightX = 113f;
        const float flightY = -126f;
        var duration = TimeSpan.FromMilliseconds(2200);
        var ease = EaseOut();

        var tokenVisual = GetVisual(SceneFileToken);
        var flight = tokenVisual.Compositor.CreateVector3KeyFrameAnimation();
        flight.Duration = duration;
        flight.IterationBehavior = AnimationIterationBehavior.Forever;
        flight.InsertKeyFrame(0f, Vector3.Zero);
        flight.InsertKeyFrame(0.55f, new Vector3(flightX * 0.72f, flightY * 0.72f - 14f, 0), ease);
        flight.InsertKeyFrame(0.78f, new Vector3(flightX, flightY, 0), ease);
        flight.InsertKeyFrame(1f, new Vector3(flightX, flightY, 0));
        tokenVisual.StartAnimation("Translation", flight);

        var tokenOpacity = tokenVisual.Compositor.CreateScalarKeyFrameAnimation();
        tokenOpacity.Duration = duration;
        tokenOpacity.IterationBehavior = AnimationIterationBehavior.Forever;
        tokenOpacity.InsertKeyFrame(0f, 0f);
        tokenOpacity.InsertKeyFrame(0.1f, 1f, ease);
        tokenOpacity.InsertKeyFrame(0.78f, 1f);
        tokenOpacity.InsertKeyFrame(0.86f, 0f, ease);
        tokenOpacity.InsertKeyFrame(1f, 0f);
        tokenVisual.StartAnimation("Opacity", tokenOpacity);

        var haloVisual = GetVisual(SceneDropBoxHalo);
        var halo = haloVisual.Compositor.CreateScalarKeyFrameAnimation();
        halo.Duration = duration;
        halo.IterationBehavior = AnimationIterationBehavior.Forever;
        halo.InsertKeyFrame(0f, 0f);
        halo.InsertKeyFrame(0.72f, 0f);
        halo.InsertKeyFrame(0.8f, 0.28f, ease);
        halo.InsertKeyFrame(0.9f, 0.06f);
        halo.InsertKeyFrame(1f, 0f);
        haloVisual.StartAnimation("Opacity", halo);

        var badgeVisual = GetVisual(SceneMoveBadge);
        var badgeFade = badgeVisual.Compositor.CreateScalarKeyFrameAnimation();
        badgeFade.Duration = duration;
        badgeFade.IterationBehavior = AnimationIterationBehavior.Forever;
        badgeFade.InsertKeyFrame(0f, 0f);
        badgeFade.InsertKeyFrame(0.76f, 0f);
        badgeFade.InsertKeyFrame(0.84f, 1f, ease);
        badgeFade.InsertKeyFrame(0.94f, 1f);
        badgeFade.InsertKeyFrame(1f, 0f);
        badgeVisual.StartAnimation("Opacity", badgeFade);

        var badgeScale = badgeVisual.Compositor.CreateVector3KeyFrameAnimation();
        badgeScale.Duration = duration;
        badgeScale.IterationBehavior = AnimationIterationBehavior.Forever;
        badgeScale.InsertKeyFrame(0f, new Vector3(0.85f, 0.85f, 1f));
        badgeScale.InsertKeyFrame(0.76f, new Vector3(0.85f, 0.85f, 1f));
        badgeScale.InsertKeyFrame(0.86f, Vector3.One, ease);
        badgeScale.InsertKeyFrame(1f, Vector3.One);
        badgeVisual.StartAnimation("Scale", badgeScale);
    }

    // ── Step 3: the keycap depresses with a soft halo ping ──

    private void StartKeycapPressLoop()
    {
        var duration = TimeSpan.FromMilliseconds(1800);
        var ease = EaseOut();

        var keycapVisual = GetVisual(SceneKeycap);
        var press = keycapVisual.Compositor.CreateVector3KeyFrameAnimation();
        press.Duration = duration;
        press.IterationBehavior = AnimationIterationBehavior.Forever;
        press.InsertKeyFrame(0f, Vector3.One);
        press.InsertKeyFrame(0.12f, new Vector3(0.9f, 0.9f, 1f), ease);
        press.InsertKeyFrame(0.28f, Vector3.One, ease);
        press.InsertKeyFrame(1f, Vector3.One);
        keycapVisual.StartAnimation("Scale", press);

        var haloVisual = GetVisual(SceneKeycapHalo);
        var halo = haloVisual.Compositor.CreateScalarKeyFrameAnimation();
        halo.Duration = duration;
        halo.IterationBehavior = AnimationIterationBehavior.Forever;
        halo.InsertKeyFrame(0f, 0f);
        halo.InsertKeyFrame(0.12f, 0.4f, ease);
        halo.InsertKeyFrame(0.58f, 0f, ease);
        halo.InsertKeyFrame(1f, 0f);
        haloVisual.StartAnimation("Opacity", halo);

        var haloScale = haloVisual.Compositor.CreateVector3KeyFrameAnimation();
        haloScale.Duration = duration;
        haloScale.IterationBehavior = AnimationIterationBehavior.Forever;
        haloScale.InsertKeyFrame(0f, new Vector3(0.9f, 0.9f, 1f));
        haloScale.InsertKeyFrame(0.58f, new Vector3(1.16f, 1.16f, 1f), ease);
        haloScale.InsertKeyFrame(1f, new Vector3(0.9f, 0.9f, 1f));
        haloVisual.StartAnimation("Scale", haloScale);
    }

    // ── Step 4: the tray icon pings under the menu ──

    private void StartTrayHaloLoop()
    {
        var duration = TimeSpan.FromMilliseconds(1400);
        var ease = EaseOut();
        var haloVisual = GetVisual(SceneTrayHalo);

        var halo = haloVisual.Compositor.CreateScalarKeyFrameAnimation();
        halo.Duration = duration;
        halo.IterationBehavior = AnimationIterationBehavior.Forever;
        halo.InsertKeyFrame(0f, 0.08f);
        halo.InsertKeyFrame(0.5f, 0.3f, ease);
        halo.InsertKeyFrame(1f, 0.08f, ease);
        haloVisual.StartAnimation("Opacity", halo);

        var haloScale = haloVisual.Compositor.CreateVector3KeyFrameAnimation();
        haloScale.Duration = duration;
        haloScale.IterationBehavior = AnimationIterationBehavior.Forever;
        haloScale.InsertKeyFrame(0f, new Vector3(0.88f, 0.88f, 1f));
        haloScale.InsertKeyFrame(0.5f, new Vector3(1.14f, 1.14f, 1f), ease);
        haloScale.InsertKeyFrame(1f, new Vector3(0.88f, 0.88f, 1f), ease);
        haloVisual.StartAnimation("Scale", haloScale);
    }

    // ── Step 5: the icon tiles idle-float with a gentle stagger ──

    private void StartFeatureFloatLoop()
    {
        var durations = new[] { 2600, 3000, 3400 };
        var index = 0;
        foreach (var child in SceneFeatureTiles.Children)
        {
            StartFloat(child, durations[index % durations.Length], (index * 130) % 700);
            index++;
        }
    }

    private void StartFloat(UIElement element, int durationMs, int delayMs)
    {
        var visual = GetVisual(element);
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var ease = EaseOut();
        var rise = visual.Compositor.CreateVector3KeyFrameAnimation();
        rise.Duration = duration;
        rise.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        rise.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        rise.IterationBehavior = AnimationIterationBehavior.Forever;
        rise.InsertKeyFrame(0f, Vector3.Zero);
        rise.InsertKeyFrame(0.5f, new Vector3(0, -3.5f, 0), ease);
        rise.InsertKeyFrame(1f, Vector3.Zero, ease);
        visual.StartAnimation("Translation", rise);
    }

    /// <summary>
    /// One-shot entrance on first paint: the halo fades in and the mark settles.
    /// </summary>
    private void PlaySceneEntrance()
    {
        if (!WindowsCompatibilityService.AreAnimationsEnabled || _stepIndex != 0)
        {
            return;
        }

        var haloVisual = GetVisual(SceneHalo);
        haloVisual.StopAnimation("Opacity");
        var haloFade = haloVisual.Compositor.CreateScalarKeyFrameAnimation();
        haloFade.Duration = TimeSpan.FromMilliseconds(600);
        haloFade.InsertKeyFrame(0f, 0f);
        haloFade.InsertKeyFrame(1f, 0.55f, EaseOut());
        haloVisual.StartAnimation("Opacity", haloFade);

        var markVisual = GetVisual(SceneMark);
        markVisual.StopAnimation("Opacity");
        markVisual.StopAnimation("Scale");
        var markFade = markVisual.Compositor.CreateScalarKeyFrameAnimation();
        markFade.Duration = TimeSpan.FromMilliseconds(420);
        markFade.DelayTime = TimeSpan.FromMilliseconds(120);
        markFade.DelayBehavior = Microsoft.UI.Composition.AnimationDelayBehavior.SetInitialValueBeforeDelay;
        markFade.InsertKeyFrame(0f, 0f);
        markFade.InsertKeyFrame(1f, 1f, EaseOut());
        markVisual.StartAnimation("Opacity", markFade);
        var markScale = markVisual.Compositor.CreateVector3KeyFrameAnimation();
        markScale.Duration = TimeSpan.FromMilliseconds(480);
        markScale.DelayTime = TimeSpan.FromMilliseconds(120);
        markScale.DelayBehavior = Microsoft.UI.Composition.AnimationDelayBehavior.SetInitialValueBeforeDelay;
        markScale.InsertKeyFrame(0f, new Vector3(0.92f, 0.92f, 1f));
        markScale.InsertKeyFrame(1f, Vector3.One, EaseOut());
        markVisual.StartAnimation("Scale", markScale);
    }

    private CubicBezierEasingFunction EaseOut()
    {
        return GetVisual(SceneHalo).Compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));
    }
}
