using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace KerboKatz
{
  [KSPAddon(KSPAddon.Startup.EditorAny, true)]
  public class CraftHistory : MonoBehaviour
  {
    private ApplicationLauncherButton button;
    private bool initStyle                                                        = false;
    private bool saveAll;
    private bool showLoadWindow;
    private bool windowCenterd                                                    = false;
    private bool workerCompleted                                                  = true;
    private Dictionary<double, int> partCount                                     = new Dictionary<double, int>();
    private Dictionary<string, string[]> historyFilesDic                          = new Dictionary<string, string[]>();
    private Dictionary<string, Tuple<string, DateTime, int, int, float, bool>> filesDic = new Dictionary<string, Tuple<string, DateTime, int, int, float, bool>>();
    private double nextCheck                                                      = 0;
    private float tooltipHeight                                                   = 0;
    private GUIStyle areaStyle;
    private GUIStyle buttonDeleteIconStyle;
    private GUIStyle buttonHistoryIconStyle;
    private GUIStyle buttonLoadIconStyle;
    private GUIStyle buttonStyle;
    private GUIStyle containerStyle;
    private GUIStyle labelStyle;
    private GUIStyle numberFieldStyle;
    private GUIStyle settingsWindowStyle;
    private GUIStyle shipNameStyle;
    private GUIStyle shipStyle;
    private GUIStyle textStyle;
    private GUIStyle toggleStyle;
    private GUIStyle tooltipStyle;
    private GUIStyle windowStyle;
    private int historyWindowID                                                   = 844526732;
    private int loadShipID                                                        = 56706112;
    private int settingsWindowID                                                  = 971199;
    private List<Exception> exceptions                                            = new List<Exception>();
    private List<Tuple<ConfigNode, string, double, string>> requestedBackups      = new List<Tuple<ConfigNode, string, double, string>>();
    private Rect historyWindow;
    private Rect settingsWindow                                                   = new Rect(0, 0, 230, 125);
    private Rect tooltipRect                                                      = new Rect(0, 0, 230, 20);
    private Rect windowPosition                                                   = new Rect(0f, 0f, 350, 505);
    private settings currentSettings;
    private string CurrentTooltip;
    private string historyFiles;
    private string modName                                                        = "CraftHistory";
    private string saveInterval;
    private string showHistory;
    private string[] files;
    private Vector2 scrollPosition                                                = new Vector2();
    private Vector2 scrollPositionHistory;
    private Version requiresUtilities                                             = new Version(1, 0, 1);
    private GUIStyle textStyleRed;

    private void Awake()
    {
      if (!Utilities.checkUtilitiesSupport(new Version(1, 0, 0), modName))
      {
        Destroy(this);
        return;
      }
      Utilities.debug(modName, "awake");
      GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
      GameEvents.onEditorShipModified.Add(onShipChange);
    }

    private void Start()
    {
      Utilities.debug(modName, "start");
      DontDestroyOnLoad(this);
      currentSettings = new settings();
      currentSettings.load(modName, "settings", modName);
      currentSettings.setDefault("saveAll", "false");
      currentSettings.setDefault("saveInterval", "1");
      currentSettings.set("editorScene", getEditorScene());
      saveAll = currentSettings.getBool("saveAll");
      saveInterval = currentSettings.getString("saveInterval");
      changePathTo(currentSettings.getString("editorScene"));

      if (!windowCenterd && windowPosition.x == 0 && windowPosition.y == 0 && windowPosition.width > 0 && windowPosition.height > 0)
      {
        windowPosition.x = Screen.width / 2 - windowPosition.width / 2;
        windowPosition.y = Screen.height / 2 - windowPosition.height / 2;
        settingsWindow.x = Screen.width;
        settingsWindow.y = Screen.height - settingsWindow.height - 38;
        windowCenterd    = true;
      }
    }

    private void OnGuiAppLauncherReady()
    {
      if (button == null)
      {
        button = ApplicationLauncher.Instance.AddModApplication(
            applauncher, 	//RUIToggleButton.onTrue
            applauncher,	//RUIToggleButton.onFalse
            null, //RUIToggleButton.OnHover
            null, //RUIToggleButton.onHoverOut
            null, //RUIToggleButton.onEnable
            null, //RUIToggleButton.onDisable
            ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB, //visibleInScenes
            Utilities.getTexture("icon", "CraftHistory/Textures")//texture
        );
      }
      if (EditorLogic.fetch.loadBtn.methodToInvoke != "toggleWindow")
      {
        EditorLogic.fetch.loadBtn.methodToInvoke = "toggleWindow";
        EditorLogic.fetch.loadBtn.scriptWithMethodToInvoke = this;
      }
      changePathTo(getEditorScene());
    }

    private void OnDestroy()
    {
      Utilities.debug(modName, "destroy");
      if (currentSettings != null)
      {
        currentSettings.set("showSettings", false);
        currentSettings.save();
      }
      GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
      GameEvents.onEditorShipModified.Remove(onShipChange);
      if (button != null)
      {
        ApplicationLauncher.Instance.RemoveModApplication(button);
      }
      while (requestedBackups.Count > 0)
      {//if for some reason the game gets ended before all crafts are saved save them before destroying
        if (workerCompleted)
        {
          backupShip();
        }
      }
    }

    private void changePathTo(string mode)
    {
      currentSettings.set("editorScene", mode);
      currentSettings.set("savePath", "saves/" + HighLogic.SaveFolder + "/Ships/" + mode + "/");
      updateShipList();
      showHistory = null;
    }

    private void toggleWindow()
    {
      Utilities.debug(modName, "Toggling window");
      if (showLoadWindow)
      {
        Utilities.debug(modName, "Hiding window");
        showLoadWindow = false;
      }
      else
      {
        updateShipList();
        Utilities.debug(modName, "Showing window");
        showLoadWindow = true;
      }
    }

    private void FixedUpdate()
    {
      if (Utilities.getUnixTimestamp() > nextCheck &&
          requestedBackups.Count > 0 &&
          workerCompleted)
      {
        updateNextCheck();
        backupShip();
      }
      if (exceptions.Count > 0)
      {
        foreach (Exception e in exceptions)
        {
          Debug.LogException(e);
        }
        exceptions.Clear();
      }
    }

    private void updateNextCheck()
    {
      if (currentSettings.getBool("saveAll"))
      {
        nextCheck = Utilities.getUnixTimestamp() + 1;
      }
      else
      {
        nextCheck = Utilities.getUnixTimestamp() + currentSettings.getDouble("saveInterval");
      }
    }

    private bool backupShip()
    {
      try
      {
        int i = 0;
        if(!currentSettings.getBool("saveAll")){
          i = requestedBackups.Count-1;
        }
        ThreadPool.QueueUserWorkItem(new WaitCallback(backgrounder), new object[] { requestedBackups[i], i});
      }
      catch (Exception e)
      {
        Debug.LogException(e);
      }
      workerCompleted = false;
      return true;
    }

    private void backgrounder(object state)
    {
      try
      {
        object[] args = state as object[];
        Tuple<ConfigNode, string, double, string> shipCopy = args[0] as Tuple<ConfigNode, string, double, string>;
        int i;
        int.TryParse(args[1].ToString(),out i);
        i++;
        saveShip(shipCopy.Item1, shipCopy.Item2, shipCopy.Item3, shipCopy.Item4);
        requestedBackups.RemoveRange(0, i);
        updateNextCheck();
        workerCompleted = true;
      }
      catch (Exception e)
      {
        exceptions.Add(e);
        workerCompleted = true;
      }
    }

    private void onShipChange(ShipConstruct ship)
    {
      if (ship.Parts.Count <= 0)
        return;
      if (!File.Exists(currentSettings.getString("savePath") + ship.shipName + ".craft"))
        return;
      var saveShip = ship.SaveShip();
      var newTuple = new Tuple<ConfigNode, string, double, string>(saveShip, ship.shipName, Utilities.getUnixTimestamp(), currentSettings.getString("savePath"));
      if (!requestedBackups.Contains(newTuple))
        requestedBackups.Add(newTuple);
      return;
    }

    private void saveShip(ConfigNode savedShip, string shipName, double timestamp, string savePath)
    {
      savePath = savePath + shipName;
      var hashedFile = savePath + "/" + timestamp + ".craft";
      Directory.CreateDirectory(savePath);
      if (!File.Exists(hashedFile))
        savedShip.Save(hashedFile);
    }

    private string getEditorScene()
    {
      if (EditorLogic.fetch.ship.shipFacility == EditorFacility.SPH)
        return "SPH";
      else
        return "VAB";
    }

    private void applauncher()
    {
      if(currentSettings.getBool("showSettings")){
        currentSettings.set("showSettings", false);
      }
      else
      {
        currentSettings.set("showSettings", true);
      }
    }

    private void loadShip(string shipFile)
    {
      EditorLogic.LoadShipFromFile(shipFile);
    }

    private void updateShipList()
    {
      Utilities.debug(modName, "Updating shiplist. Clearing dictionaries...");
      filesDic.Clear();
      historyFilesDic.Clear();
      Utilities.debug(modName, "Done clearing dictionaries. Getting files...");
      files = getFiles(currentSettings.getString("savePath"));
      Utilities.debug(modName, "Done getting files." + files.Length+ " files found. Looping through files...");
      foreach (string file in files)
      {
        Utilities.debug(modName, "->Adding file to dictionary...");
        addToFilesDic(file);
        Utilities.debug(modName, "->Done! Checking for history...");
        var craftFileName = filesDic[file].Item1;
        historyFilesDic.Add(file, Utilities.reverseArray(getFiles(currentSettings.getString("savePath") + craftFileName + "/")));
        Utilities.debug(modName, "->Done! " + historyFilesDic[file].Length + " files found. Looping through files...");
        foreach (string hFile in historyFilesDic[file])
        {
          Utilities.debug(modName, "->->Adding file to dictionary...");
          addToFilesDic(hFile);
          Utilities.debug(modName, "->->Done!");
        }
        Utilities.debug(modName, "->Done!");
      }

      Utilities.debug(modName, "Done!");
    }

    private string[] getFiles(string path)
    {
      if (Directory.Exists(path))
        return Directory.GetFiles(path, "*.craft", SearchOption.TopDirectoryOnly);
      else
        return new string[0];
    }

    private void addToFilesDic(string file)
    {
      if (!filesDic.ContainsKey(file))
      {
        int partCount, stageCount;
        float vesselCost;
        bool vesselComplete;
        getCraftInfo(file, out partCount, out stageCount, out vesselCost, out vesselComplete);
        FileInfo fileInfo = new FileInfo(file);
        filesDic.Add(file, new Tuple<string, DateTime, int, int, float, bool>(fileInfo.Name.Replace(".craft", ""), fileInfo.LastWriteTime, partCount, stageCount, vesselCost, vesselComplete));
      }
    }

    private static void getCraftInfo(string file, out int partCount, out int stageCount, out float vesselCost,out bool vesselComplete)
    {
      var nodes = ConfigNode.Load(file).GetNodes("PART");
      partCount = nodes.Length;
      Utilities.getVesselCostAndStages(nodes, out stageCount, out vesselCost, out vesselComplete);
    }

    #region ui
    private void OnGUI()
    {
      if (!initStyle)
        InitStyle();
      if (HighLogic.LoadedSceneIsEditor)
      {
        if (currentSettings.getBool("showSettings"))//settings window
        {
          settingsWindow = GUILayout.Window(settingsWindowID, settingsWindow, createSettingsWindow, "CraftHistory Settings", settingsWindowStyle);
          Utilities.clampToScreen(ref settingsWindow);
          Utilities.lockEditor(settingsWindow, settingsWindowID.ToString());
        }
        else
        {
          EditorLogic.fetch.Unlock(settingsWindowID.ToString());
        }
        if (showLoadWindow)//load window
        {
          windowPosition = GUILayout.Window(loadShipID, windowPosition, MainWindow, "Select a ship to load", windowStyle);
          Utilities.clampToScreen(ref windowPosition);
          Utilities.lockEditor(windowPosition, loadShipID.ToString());
        }
        else
        {
          EditorLogic.fetch.Unlock(loadShipID.ToString());
        }
        if (!String.IsNullOrEmpty(showHistory))//history window
        {
          historyWindow = GUILayout.Window(historyWindowID, historyWindow, createHistoryWindow, "Select a ship from history to load", windowStyle);
          Utilities.clampToScreen(ref historyWindow);
          Utilities.lockEditor(historyWindow, historyWindowID.ToString());
        }
        else
        {
          EditorLogic.fetch.Unlock(historyWindowID.ToString());
        }

        if (!String.IsNullOrEmpty(CurrentTooltip))
        {
          tooltipRect.x = Input.mousePosition.x + 10;
          tooltipRect.y = Screen.height - Input.mousePosition.y + 10;
          Utilities.clampToScreen(ref tooltipRect);
          tooltipRect.height = tooltipHeight;
          GUI.Label(tooltipRect, CurrentTooltip, tooltipStyle);
          GUI.depth = 0;
        }
      }
    }

    private void InitStyle()
    {
      labelStyle                               = new GUIStyle(HighLogic.Skin.label);
      labelStyle.stretchWidth = true;

      windowStyle                              = new GUIStyle(HighLogic.Skin.window);
      windowStyle.fixedWidth                   = 350;
      windowStyle.padding.left                 = 0;
      windowStyle.fixedHeight                  = 505;

      settingsWindowStyle                      = new GUIStyle(HighLogic.Skin.window);
      settingsWindowStyle.fixedWidth           = 250;
      settingsWindowStyle.padding.left         = 0;

      toggleStyle                              = new GUIStyle(HighLogic.Skin.toggle);
      toggleStyle.normal.textColor             = labelStyle.normal.textColor;
      toggleStyle.active.textColor             = labelStyle.normal.textColor;

      textStyle                                = new GUIStyle(HighLogic.Skin.label);
      textStyle.fixedWidth                     = 150;
      textStyle.margin.left                    = 10;
      textStyleRed                             = new GUIStyle(textStyle);
      textStyleRed.normal.textColor            = Color.red;

      shipStyle                                = new GUIStyle(HighLogic.Skin.label);
      shipStyle.fixedWidth                     = 270;
      shipStyle.margin.left                    = 10;

      shipNameStyle                            = new GUIStyle(HighLogic.Skin.label);
      shipNameStyle.fixedWidth                 = 270;
      shipNameStyle.margin.left                = 10;
      shipNameStyle.fontStyle                  = FontStyle.Bold;

      containerStyle                           = new GUIStyle(GUI.skin.button);
      containerStyle.fixedWidth                = 230;
      containerStyle.margin.left               = 10;

      numberFieldStyle                         = new GUIStyle(HighLogic.Skin.box);
      numberFieldStyle.fixedWidth              = 52;
      numberFieldStyle.fixedHeight             = 22;
      numberFieldStyle.alignment               = TextAnchor.MiddleCenter;
      numberFieldStyle.margin.left             = 45;
      numberFieldStyle.padding.right           = 7;
      numberFieldStyle.margin.top              = 4;

      buttonDeleteIconStyle                    = new GUIStyle(GUI.skin.button);
      buttonDeleteIconStyle.fixedWidth         = 38;
      buttonDeleteIconStyle.fixedHeight        = 38;
      buttonDeleteIconStyle.normal.background  = Utilities.getTexture("button_Delete", "CraftHistory/Textures");
      buttonDeleteIconStyle.hover.background   = Utilities.getTexture("button_Delete_mouseover", "CraftHistory/Textures");
      buttonDeleteIconStyle.active             = buttonDeleteIconStyle.hover;

      buttonHistoryIconStyle                   = new GUIStyle(buttonDeleteIconStyle);
      buttonHistoryIconStyle.normal.background = Utilities.getTexture("button_History", "CraftHistory/Textures");
      buttonHistoryIconStyle.hover.background  = Utilities.getTexture("button_History_mouseover", "CraftHistory/Textures");
      buttonHistoryIconStyle.active            = buttonHistoryIconStyle.hover;

      buttonLoadIconStyle                      = new GUIStyle(buttonDeleteIconStyle);
      buttonLoadIconStyle.normal.background    = Utilities.getTexture("button_Load", "CraftHistory/Textures");
      buttonLoadIconStyle.hover.background     = Utilities.getTexture("button_Load_mouseover", "CraftHistory/Textures");
      buttonLoadIconStyle.active               = buttonLoadIconStyle.hover;

      buttonStyle                              = new GUIStyle(GUI.skin.button);
      buttonStyle.fixedWidth                   = 100;
      buttonStyle.alignment                    = TextAnchor.MiddleCenter;

      areaStyle                                = new GUIStyle(HighLogic.Skin.button);
      areaStyle.fixedWidth                     = 330;
      areaStyle.onHover                        = areaStyle.normal;
      areaStyle.hover                          = areaStyle.normal;
      
      tooltipStyle                             = new GUIStyle(HighLogic.Skin.label);
      tooltipStyle.fixedWidth                  = 230;
      tooltipStyle.padding.top                 = 5;
      tooltipStyle.padding.left                = 5;
      tooltipStyle.padding.right               = 5;
      tooltipStyle.padding.bottom              = 5;
      tooltipStyle.fontSize                    = 10;
      tooltipStyle.normal.background           = Utilities.getTexture("tooltipBG", "Textures");
      tooltipStyle.normal.textColor            = Color.white;
      tooltipStyle.border.top                  = 1;
      tooltipStyle.border.bottom               = 1;
      tooltipStyle.border.left                 = 8;
      tooltipStyle.border.right                = 8;
      tooltipStyle.stretchHeight               = true;

      initStyle                                = true;
    }

    private void createSettingsWindow(int id)
    {
      GUILayout.BeginVertical();
      if (GUILayout.Toggle(saveAll, new GUIContent("Save every iteration of the ship", "Disable to only save the latest iteration of the ship after X seconds"), toggleStyle))
      {
        saveAll = true;
      }
      else
      {
        saveAll = false;
      }
      GUI.enabled = !saveAll;
      GUILayout.BeginHorizontal();
      Utilities.createLabel("Save Interval (sec):", textStyle, "Saves the ship after these seconds");
      saveInterval = Utilities.getOnlyNumbers(GUILayout.TextField(saveInterval, 5, numberFieldStyle));
      GUILayout.EndHorizontal();
      GUI.enabled = true;
      GUILayout.Space(10);
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("Save", buttonStyle))
      {
        currentSettings.set("saveAll", saveAll);
        currentSettings.set("saveInterval", saveInterval);
        currentSettings.save();
        currentSettings.set("showSettings", false);
      }
      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
      GUI.DragWindow();
      tooltipHeight = tooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), tooltipStyle.fixedWidth);
      CurrentTooltip = GUI.tooltip;
    }

    private void MainWindow(int windowID)
    {
      GUILayout.BeginVertical();

      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      if (Utilities.createButton("VAB", buttonStyle, (currentSettings.getString("editorScene") == "VAB")))
      {
        changePathTo("VAB");
      }
      if (Utilities.createButton("SPH", buttonStyle, (currentSettings.getString("editorScene") == "SPH")))
      {
        changePathTo("SPH");
      }
      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();
      scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(350), GUILayout.Height(420));
      foreach (string file in files)
      {
        var craftFileName  = filesDic[file].Item1;
        var craftEditTime  = filesDic[file].Item2;
        var craftPartCount = filesDic[file].Item3;
        var craftStages    = filesDic[file].Item4;
        var craftCost      = filesDic[file].Item5;
        var vesselComplete = filesDic[file].Item6;
        GUILayout.BeginVertical(areaStyle);
        GUILayout.BeginHorizontal();
        createCraftInfo(craftFileName, craftEditTime, craftPartCount, craftStages, craftCost, vesselComplete);
        GUILayout.BeginVertical();

        string historyPath = currentSettings.getString("savePath") + craftFileName + "/";
        if (Utilities.createButton("", buttonHistoryIconStyle, (!historyFilesDic.ContainsKey(file) || historyFilesDic[file].Length <= 0)))
        {
          if (showHistory  == historyPath)
            showHistory    = null;
          else
            showHistory    = historyPath;
          historyFiles     = file;
          historyWindow.x  = Input.mousePosition.x;
          historyWindow.y  = Screen.height - Input.mousePosition.y;
          GUI.BringWindowToFront(844526732);
        }
        createCraftLoadButton(file, vesselComplete);
        createCraftDeleteButton(file, historyPath);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
      }
      GUILayout.EndScrollView();
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      if (Utilities.createButton("Close", buttonStyle))
      {
        showLoadWindow     = false;
        showHistory        = null;
      }
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
      GUI.DragWindow();
      tooltipHeight        = tooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), tooltipStyle.fixedWidth);
      CurrentTooltip       = GUI.tooltip;
    }

    private void createCraftDeleteButton(string file, string historyPath = null)
    {
      if (Utilities.createButton("", buttonDeleteIconStyle))
      {
        if (File.Exists(file))
          File.Delete(file);
        deleteHistory(historyPath);
      }
    }

    private void deleteHistory(string historyPath)
    {
      if (!string.IsNullOrEmpty(historyPath))
      {
        if (showHistory == historyPath)
          showHistory = null;
        if (Directory.Exists(historyPath))
          Directory.Delete(historyPath, true);
      }
      updateShipList();
    }

    private void createCraftLoadButton(string file, bool vesselComplete)
    {
      if (Utilities.createButton("", buttonLoadIconStyle, !vesselComplete))
      {
        loadShip(file);
        showLoadWindow = false;
        showHistory = null;
      }
    }

    private void createCraftInfo(string craftFileName, DateTime craftEditTime, int craftPartCount, int craftStages, float craftCost, bool vesselComplete, bool hideDate = false)
    {
      GUILayout.BeginVertical();
      Utilities.createLabel(craftFileName, shipNameStyle);
      if (!hideDate)
        Utilities.createLabel(craftEditTime.ToString("yyyy.MM.dd HH:mm:ss"), shipStyle);

      Utilities.createLabel(getPartAndStageString(craftPartCount, "Part", false) + " in " + getPartAndStageString(craftStages, "Stage"), shipStyle);
      Utilities.createLabel("Craft cost: " + craftCost.ToString("N0"), shipStyle);
      if (!vesselComplete)
        Utilities.createLabel("Craft is missing Parts", textStyleRed);
      GUILayout.EndVertical();
    }

    private static string getPartAndStageString(int count, string p, bool zeroNumber = true)
    {
      if (count > 1)
      {
        return count + " " + p + "s";
      }
      else if (count == 1)
      {
        return "1 " + p;
      }
      else if (count == 0 && zeroNumber)
      {
        return "0 " + p + "s";
      }
      else
      {
        return "No " + p + "s";
      }
    }

    private void createHistoryWindow(int windowID)
    {
      GUILayout.BeginVertical();

      scrollPositionHistory = GUILayout.BeginScrollView(scrollPositionHistory, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(350), GUILayout.Height(445));
      foreach (string file in historyFilesDic[historyFiles])
      {
        var craftFileName  = filesDic[file].Item1;
        var craftEditTime  = filesDic[file].Item2;
        var craftPartCount = filesDic[file].Item3;
        var craftStages    = filesDic[file].Item4;
        var craftCost      = filesDic[file].Item5;
        var vesselComplete = filesDic[file].Item6;
        GUILayout.BeginVertical(areaStyle);
        GUILayout.BeginHorizontal();
        double craftTime   = 0;
        double.TryParse(craftFileName, out craftTime);
        craftFileName      = Utilities.convertUnixTimestampToDate(craftTime).ToString("yyyy.MM.dd HH:mm:ss");
        createCraftInfo(craftFileName, craftEditTime, craftPartCount, craftStages, craftCost, vesselComplete, true);
        GUILayout.BeginVertical();
        createCraftLoadButton(file, vesselComplete);
        createCraftDeleteButton(file);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
      }
      GUILayout.EndScrollView();
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      if (Utilities.createButton("Delete history", buttonStyle))
      {
        deleteHistory(showHistory);
      }
      if (Utilities.createButton("Close", buttonStyle))
      {
        showHistory        = null;
      }
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
      GUI.DragWindow();
    }

    #endregion ui
  }
}