using System;

namespace BronzebeardHud.Stats;

/// <summary>
/// Isolates one plugin feature from the others. HDT calls the plugin's OnUpdate about ten times a
/// second; an unexpected exception in one feature (say a HearthMirror type that changed between the HDT
/// version we compile against and the one installed: MissingMethodException, TypeLoadException) would
/// otherwise break every feature on every tick. The first unexpected exception disables this feature
/// alone and is reported once; later calls do nothing.
///
/// Transient failures are not unexpected and only skip the current tick: InvalidOperationException
/// (HDT mutating a collection while we read it, the adapter's usual retry case) and
/// OperationCanceledException.
/// </summary>
public sealed class FeatureGuard
{
    private readonly Action<string, Exception> _onFailure;

    /// <param name="name">Feature name, as it will appear in the log.</param>
    /// <param name="onFailure">Called once, with the name and the exception, when the feature gets disabled.</param>
    public FeatureGuard(string name, Action<string, Exception> onFailure)
    {
        Name = name;
        _onFailure = onFailure;
    }

    public string Name { get; }

    public bool IsDisabled { get; private set; }

    public void Run(Action action)
    {
        if (IsDisabled)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
        {
            // Transient: try again on the next tick.
        }
        catch (Exception e)
        {
            IsDisabled = true;
            _onFailure(Name, e);
        }
    }
}
