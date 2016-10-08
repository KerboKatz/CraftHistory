namespace KerboKatz.CH
{
  public enum SortOptions
  {
    Name,
    Parts,
    Stages,
    Cost
  }

  public enum BackupInterval
  {
    OnSave,
    OnChange,
    Timed,
    Disabled
  }

  public class Settings : SettingsBase<Settings>
  {
    public bool isShips = true;
    public bool isVAB = true;
    internal bool showLoadCrafts = false;
    public SortOptions sortOption = SortOptions.Name;
    public BackupInterval backupInterval = BackupInterval.OnSave;
    public bool showSettings;
    public bool ascending = true;
    public bool disableHistory;
    public float backupDelay = 10;
    public bool hideUnloadable;
    public float uiScale = 100;
    public bool isStock = false;
  }
}