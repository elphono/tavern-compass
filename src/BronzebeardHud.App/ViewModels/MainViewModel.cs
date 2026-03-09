using System.ComponentModel;
using System.Runtime.CompilerServices;
using BronzebeardHud.App.Services;
using BronzebeardHud.GameState;

namespace BronzebeardHud.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private GameStateSnapshot _state = new();
    private int _imageVersion;

    public MainViewModel()
    {
        // When a card image finishes downloading, bump version to trigger re-render
        try
        {
            CardImageCache.Instance.ImageLoaded += _ =>
            {
                _imageVersion++;
                OnPropertyChanged(nameof(ImageVersion));
            };
        }
        catch
        {
            // CardImageCache may not be available in test environments without Avalonia
        }
    }

    public int ImageVersion => _imageVersion;

    public GameStateSnapshot State
    {
        get => _state;
        set { _state = value; OnPropertyChanged(); OnPropertyChanged(nameof(PhaseText)); OnPropertyChanged(nameof(IsInGame)); OnPropertyChanged(nameof(IsShopping)); }
    }

    public bool IsInGame => State.Phase is GamePhase.Shopping or GamePhase.Combat or GamePhase.GameOver;
    public bool IsShopping => State.Phase == GamePhase.Shopping;

    public string PhaseText => State.Phase switch
    {
        GamePhase.NotStarted => "Waiting for game...",
        GamePhase.HeroSelect => "Hero selection...",
        GamePhase.Shopping => $"Turn {State.Turn} - Shopping",
        GamePhase.Combat => $"Turn {State.Turn} - Combat",
        GamePhase.GameOver => "Game Over",
        _ => "",
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
