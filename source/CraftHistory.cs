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
    private bool disableAutoSave;
    private bool hideUnloadableCrafts;
    private bool initStyle                                                              = false;
    private bool saveAll;
    private bool showLoadWindow;
    private bool windowCenterd                                                          = false;
    private bool workerCompleted                                                        = true;
    private Dictionary<double, int> partCount                                           = new Dictionary<double, int>();
    private Dictionary<string, bool> toggleCategories                                   = new Dictionary<string, bool>();
    private Dictionary<string, string[]> historyFilesDic                                = new Dictionary<string, string[]>();
    private Dictionary<string, Tuple<string, DateTime, int, int, float, bool>> filesDic = new Dictionary<string, Tuple<string, DateTime, int, int, float, bool>>();
    private double nextCheck                                                            = 0;
    private float tooltipHeight                                                         = 0;
    private GUIStyle areaStyle;
    private GUIStyle arrowStyle;
    private GUIStyle buttonDeleteIconStyle;
    private GUIStyle buttonHistoryIconStyle;
    private GUIStyle buttonLoadIconStyle;
    private GUIStyle buttonStyle;
    private GUIStyle categoryStyle;
    private GUIStyle categoryTextStyle;
    private GUIStyle containerStyle;
    private GUIStyle craftNameStyle;
    private GUIStyle craftStyle;
    private GUIStyle labelStyle;
    private GUIStyle loadWindowStyle;
    private GUIStyle numberFieldStyle;
    private GUIStyle settingsWindowStyle;
    private GUIStyle textStyle;
    private GUIStyle textStyleRed;
    private GUIStyle toggleStyle;
    private GUIStyle tooltipStyle;
    private int historyWindowID                                                         = 844526732;
    private int loadCraftID                                                             = 56706112;
    private int settingsWindowID                                                        = 971199;
    private List<Exception> exceptions                                                  = new List<Exception>();
    private List<KeyValuePair<string, string>> files                                    = new List<KeyValuePair<string, string>>();
    private List<Tuple<ConfigNode, string, double, string>> requestedBackups            = new List<Tuple<ConfigNode, string, double, string>>();
    private Rect historyWindow;
    private Rect loadWindowPosition                                                     = new Rect(0f, 0f, 350, 505);
    private Rect settingsWindow                                                         = new Rect(0, 0, 230, 225);
    private Rect tooltipRect                                                            = new Rect(0, 0, 230, 20);
    private settings currentSettings;
    private string CurrentTooltip;
    private string delimiter;
    private string historyFiles;
    private string modName                                                              = "CraftHistory";
    private string saveInterval;
    private string showHistory;
    private Vector2 scrollPosition                                                      = new Vector2();
    private Vector2 scrollPositionHistory;
    private Version requiresUtilities                                                   = new Version(1, 0, 2);

    private void Awake()
    {
      if (!Utilities.checkUtilitiesSupport(requiresUtilities, modName))
      {
        Destroy(this);
        return;
      }
      Utilities.debug(modName, "awake");
      GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
      GameEvents.onEditorShipModified.Add(onCraftChange);
    }

    private void Start()
    {
      Utilities.debug(modName, "start");
      DontDestroyOnLoad(this);
      currentSettings = new settings();
      currentSettings.load(modName, "settings", modName);
      currentSettings.setDefault("saveAll", "false");
      currentSettings.setDefault("saveInterval", "1");
      currentSettings.setDefault("hideUnloadableCrafts", "true");
      currentSettings.setDefault("delimiter", ";");
      currentSettings.setDefault("disableAutoSave", "false");
      currentSettings.set("editorScene", getEditorScene());
      hideUnloadableCrafts = currentSettings.getBool("hideUnloadableCrafts");
      disableAutoSave = currentSettings.getBool("disableAutoSave");
      saveAll = currentSettings.getBool("saveAll");
      saveInterval = currentSettings.getString("saveInterval");
      delimiter = currentSettings.getString("delimiter");
      changePathTo(currentSettings.getString("editorScene"));

      if (!windowCenterd && loadWindowPosition.x == 0 && loadWindowPosition.y == 0 && loadWindowPosition.width > 0 && loadWindowPosition.height > 0)
      {
        loadWindowPosition.x = Screen.width / 2 - loadWindowPosition.width / 2;
        loadWindowPosition.y = Screen.height / 2 - loadWindowPosition.height / 2;
        settingsWindow.x = currentSettings.getFloat("windowX");
        settingsWindow.y = currentSettings.getFloat("windowY");
        if (settingsWindow.x == 0 && settingsWindow.y == 0)
        {
          settingsWindow.x = Screen.width;
          settingsWindow.y = Screen.height - settingsWindow.height - 38;
        }
        windowCenterd = true;
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
        currentSettings.set("showSettings", false);
        currentSettings.set("windowX", settingsWindow.x);
        currentSettings.set("windowY", settingsWindow.y);
        currentSettings.save();
      }
      GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
      GameEvents.onEditorShipModified.Remove(onCraftChange);
      if (button != null)
      {
        ApplicationLauncher.Instance.RemoveModApplication(button);
      }
      while (requestedBackups.Count > 0)
      {//if for some reason the game gets ended before all crafts are saved save them before destroying
        if (workerCompleted)
        {
          backupCraft();
        }
      }
    }

    private void changePathTo(string mode)
    {
      currentSettings.set("editorScene", mode);
      currentSettings.set("savePath", "saves/" + HighLogic.SaveFolder + "/Ships/" + mode + "/");
      updateCraftList();
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
        updateCraftList();
        Utilities.debug(modName, "Showing window");
        showLoadWindow = true;
      }
    }

    private void FixedUpdate()
    {
      if (!currentSettings.getBool("disableAutoSave") &&
          Utilities.getUnixTimestamp() > nextCheck &&
          requestedBackups.Count > 0 &&
          workerCompleted)
      {
        updateNextCheck();
        backupCraft();
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

    private bool backupCraft()
    {
      try
      {
        int i = 0;
        if (!currentSettings.getBool("saveAll"))
        {
          i = requestedBackups.Count - 1;
        }
        ThreadPool.QueueUserWorkItem(new WaitCallback(backgrounder), new object[] { requestedBackups[i], i });
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
        int.TryParse(args[1].ToString(), out i);
        i++;
        saveCraft(shipCopy.Item1, shipCopy.Item2, shipCopy.Item3, shipCopy.Item4);
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

    private void onCraftChange(ShipConstruct craft)
    {
      if (currentSettings.getBool("disableAutoSave"))
        return;
      if (craft.Parts.Count <= 0)
        return;
      if (!File.Exists(currentSettings.getString("savePath") + craft.shipName + ".craft"))
        return;
      var saveCraft = craft.SaveShip();
      var newTuple = new Tuple<ConfigNode, string, double, string>(saveCraft, craft.shipName, Utilities.getUnixTimestamp(), currentSettings.getString("savePath"));
      if (!requestedBackups.Contains(newTuple))
        requestedBackups.Add(newTuple);
      return;
    }

    private void saveCraft(ConfigNode savedCraft, string craftName, double timestamp, string savePath)
    {
      savePath = savePath + craftName;
      var hashedFile = savePath + "/" + timestamp + ".craft";
      Directory.CreateDirectory(savePath);
      if (!File.Exists(hashedFile))
        savedCraft.Save(hashedFile);
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
      if (currentSettings.getBool("showSettings"))
      {
        currentSettings.set("showSettings", false);
      }
      else
      {
        currentSettings.set("showSettings", true);
      }
    }

    private void loadCraft(string craftFile)
    {
      EditorLogic.LoadShipFromFile(craftFile);
    }

    private void updateCraftList()
    {
      Utilities.debug(modName, "Updating craftlist. Clearing dictionaries...");
      filesDic.Clear();
      historyFilesDic.Clear();
      files.Clear();
      Utilities.debug(modName, "Done clearing dictionaries. Getting files...");
      var filesArray = getFiles(currentSettings.getString("savePath"));
      Utilities.debug(modName, "Done getting files." + filesArray.Length + " files found. Looping through files...");
      foreach (string file in filesArray)
      {
        Utilities.debug(modName, "->Adding file to dictionary...");
        addToFilesDic(file);
        Utilities.debug(modName, "->Done! Checking for history...");
        string catergory = "";
        var craftFileName = filesDic[file].Item1;
        if (!string.IsNullOrEmpty(currentSettings.getString("delimiter")))
        {
          var categories = craftFileName.Split(currentSettings.getString("delimiter").ToCharArray(), 2, StringSplitOptions.None);
          if (categories[0] != craftFileName)
          {
            catergory = categories[0];
            filesDic[file].Item1 = categories[1];
          }
          Utilities.debug(modName, "->Seting category as " + catergory);
        }
        Utilities.debug(modName, "->Set category as " + catergory);
        files.Add(new KeyValuePair<string, string>(file, catergory));
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
      files.Sort((x, y) =>
      {
        Utilities.debug(modName, x.Value + "= -> =" + y.Value);
        if (String.IsNullOrEmpty(y.Value) && !String.IsNullOrEmpty(x.Value))
        {
          return -1;
        }
        else if (!String.IsNullOrEmpty(y.Value) && String.IsNullOrEmpty(x.Value))
        {
          return 1;
        }
        else
        {
          return x.Value.CompareTo(y.Value);
        }
      });
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
        float craftCost;
        bool craftComplete;
        getCraftInfo(file, out partCount, out stageCount, out craftCost, out craftComplete);
        FileInfo fileInfo = new FileInfo(file);
        filesDic.Add(file, new Tuple<string, DateTime, int, int, float, bool>(fileInfo.Name.Replace(".craft", ""), fileInfo.LastWriteTime, partCount, stageCount, craftCost, craftComplete));
      }
    }

    private static void getCraftInfo(string file, out int partCount, out int stageCount, out float craftCost, out bool craftComplete)
    {
      var nodes = ConfigNode.Load(file).GetNodes("PART");
      partCount = nodes.Length;
      Utilities.getCraftCostAndStages(nodes, out stageCount, out craftCost, out craftComplete);
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
          loadWindowPosition = GUILayout.Window(loadCraftID, loadWindowPosition, loadWindow, "Select a craft to load", loadWindowStyle);
          Utilities.clampToScreen(ref loadWindowPosition);
          Utilities.lockEditor(loadWindowPosition, loadCraftID.ToString());
        }
        else
        {
          EditorLogic.fetch.Unlock(loadCraftID.ToString());
        }
        if (!String.IsNullOrEmpty(showHistory))//history window
        {
          historyWindow = GUILayout.Window(historyWindowID, historyWindow, createHistoryWindow, "Select a craft from history to load", loadWindowStyle);
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
      labelStyle.stretchWidth                  = true;

      loadWindowStyle                          = new GUIStyle(HighLogic.Skin.window);
      loadWindowStyle.fixedWidth               = 350;
      loadWindowStyle.padding.left             = 0;
      loadWindowStyle.fixedHeight              = 505;

      settingsWindowStyle                      = new GUIStyle(HighLogic.Skin.window);
      settingsWindowStyle.fixedWidth           = 250;
      settingsWindowStyle.padding.left         = 0;

      toggleStyle                              = new GUIStyle(HighLogic.Skin.toggle);
      toggleStyle.normal.textColor             = labelStyle.normal.textColor;
      toggleStyle.active.textColor             = labelStyle.normal.textColor;

      arrowStyle                               = new GUIStyle(toggleStyle);
      arrowStyle.active.background             = Utilities.getTexture("CraftHistoryToggle_off_hover", "CraftHistory/Textures");
      arrowStyle.onActive.background           = Utilities.getTexture("CraftHistoryToggle_on_hover", "CraftHistory/Textures");
      arrowStyle.normal.background             = Utilities.getTexture("CraftHistoryToggle_off", "CraftHistory/Textures");
      arrowStyle.onNormal.background           = Utilities.getTexture("CraftHistoryToggle_on", "CraftHistory/Textures");
      arrowStyle.hover.background              = Utilities.getTexture("CraftHistoryToggle_off_hover", "CraftHistory/Textures");
      arrowStyle.onHover.background            = Utilities.getTexture("CraftHistoryToggle_on_hover", "CraftHistory/Textures");

      arrowStyle.fixedHeight                   = 20;
      arrowStyle.fixedWidth                    = 20;
      arrowStyle.overflow.bottom               = 0;
      arrowStyle.overflow.top                  = 0;
      arrowStyle.overflow.left                 = 0;
      arrowStyle.overflow.right                = 0;
      arrowStyle.padding.top                   = 0;
      arrowStyle.padding.left                  = 0;
      arrowStyle.padding.right                 = 0;
      arrowStyle.padding.bottom                = 0;

      textStyle                                = new GUIStyle(HighLogic.Skin.label);
      textStyle.fixedWidth                     = 150;
      textStyle.margin.left                    = 10;
      textStyleRed                             = new GUIStyle(textStyle);
      textStyleRed.normal.textColor            = Color.red;

      craftStyle                               = new GUIStyle(HighLogic.Skin.label);
      craftStyle.fixedWidth                    = 260;
      craftStyle.margin.left                   = 10;

      craftNameStyle                           = new GUIStyle(craftStyle);
      craftNameStyle.fixedWidth                = 240;
      craftNameStyle.margin.left               = 5;
      craftNameStyle.margin.top                = 0;
      craftNameStyle.fontStyle                 = FontStyle.Bold;

      categoryTextStyle                        = new GUIStyle(craftStyle);
      categoryTextStyle.fontStyle              = FontStyle.Bold;
      categoryTextStyle.padding.top            = 0;
      categoryTextStyle.margin.top             = 2;

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
      buttonDeleteIconStyle.margin.top         = 0;
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

      categoryStyle                            = new GUIStyle(HighLogic.Skin.button);
      categoryStyle.fixedWidth                 = 330;
      categoryStyle.onHover                    = categoryStyle.normal;
      categoryStyle.hover                      = categoryStyle.normal;

      areaStyle                                = new GUIStyle(HighLogic.Skin.button);
      areaStyle.fixedWidth                     = 320;
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
      if (GUILayout.Toggle(disableAutoSave, new GUIContent("Disable CraftHistory", "If you enable this option you still will be able to use the category features without creating any history files of your crafts."), toggleStyle))
      {
        disableAutoSave = true;
      }
      else
      {
        disableAutoSave = false;
      }
      if (GUILayout.Toggle(saveAll, new GUIContent("Save every iteration of the craft", "Disable to only save the latest iteration of the craft after X seconds"), toggleStyle))
      {
        saveAll = true;
      }
      else
      {
        saveAll = false;
      }
      GUILayout.BeginHorizontal();
      GUI.enabled = !saveAll;
      Utilities.createLabel("Save Interval (sec):", textStyle, "Saves the craft after these seconds");
      saveInterval = Utilities.getOnlyNumbers(GUILayout.TextField(saveInterval, 5, numberFieldStyle));
      GUI.enabled = true;
      GUILayout.EndHorizontal();

      if (GUILayout.Toggle(hideUnloadableCrafts, new GUIContent("Hide unloadable craft"), toggleStyle))
      {
        hideUnloadableCrafts = true;
      }
      else
      {
        hideUnloadableCrafts = false;
      }

      GUILayout.BeginHorizontal();
      Utilities.createLabel("Delimiter:", textStyle, "Save crafts with a prefix and this delimter to create categories.");
      delimiter = GUILayout.TextField(delimiter, 1, numberFieldStyle);
      GUILayout.EndHorizontal();

      GUILayout.Space(10);
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("Save", buttonStyle))
      {
        currentSettings.set("disableAutoSave", disableAutoSave);
        currentSettings.set("saveAll", saveAll);
        currentSettings.set("hideUnloadableCrafts", hideUnloadableCrafts);
        currentSettings.set("saveInterval", saveInterval);
        currentSettings.set("delimiter", delimiter);
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

    private void loadWindow(int windowID)
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
      var currentCategory = "";
      var startedCategory = false;
      foreach (KeyValuePair<string, string> pair in files)
      {
        var file = pair.Key;
        var category = pair.Value;
        var craftFileName = filesDic[file].Item1;
        var craftEditTime = filesDic[file].Item2;
        var craftPartCount = filesDic[file].Item3;
        var craftStages = filesDic[file].Item4;
        var craftCost = filesDic[file].Item5;
        var craftComplete = filesDic[file].Item6;
        if (!craftComplete && currentSettings.getBool("hideUnloadableCrafts"))
          continue;
        if (!string.IsNullOrEmpty(category))
        {
          if (category != currentCategory)
          {
            if (startedCategory)
            {
              GUILayout.EndVertical();
            }
            if (!toggleCategories.ContainsKey(category))
              toggleCategories.Add(category, false);
            GUILayout.BeginVertical(categoryStyle);
            currentCategory = category;
            startedCategory = true;
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(toggleCategories[category], new GUIContent("", "Show/hide " + currentCategory), arrowStyle))
            {
              toggleCategories[category] = true;
            }
            else
            {
              toggleCategories[category] = false;
            }
            Utilities.createLabel(currentCategory, categoryTextStyle);
            GUILayout.EndHorizontal();
          }
          if (toggleCategories.ContainsKey(category) && !toggleCategories[category])
            continue;
        }
        else
        {
          if (startedCategory)
          {
            GUILayout.EndVertical();
            startedCategory = false;
          }
        }
        GUILayout.BeginVertical(areaStyle);
        GUILayout.BeginHorizontal();
        bool show = createCraftInfo(file, craftFileName, craftEditTime, craftPartCount, craftStages, craftCost, craftComplete);
        GUILayout.BeginVertical();
        createCraftLoadButton(file, craftComplete);
        if (show)
        {
          string historyPath = currentSettings.getString("savePath") + craftFileName + "/";
          if (Utilities.createButton("", buttonHistoryIconStyle, (!historyFilesDic.ContainsKey(file) || historyFilesDic[file].Length <= 0)))
          {
            if (showHistory == historyPath)
              showHistory = null;
            else
              showHistory = historyPath;
            historyFiles = file;
            historyWindow.x = Input.mousePosition.x;
            historyWindow.y = Screen.height - Input.mousePosition.y;
            GUI.BringWindowToFront(844526732);
          }
          createCraftDeleteButton(file, historyPath);
        }
        GUILayout.Space(0);
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
      }
      if (startedCategory)
      {
        GUILayout.EndVertical();
      }
      GUILayout.EndScrollView();
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      if (Utilities.createButton("Close", buttonStyle))
      {
        showLoadWindow = false;
        showHistory = null;
      }
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
      GUI.DragWindow();
      tooltipHeight = tooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), tooltipStyle.fixedWidth);
      CurrentTooltip = GUI.tooltip;
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
      updateCraftList();
    }

    private void createCraftLoadButton(string file, bool craftComplete)
    {
      if (Utilities.createButton("", buttonLoadIconStyle, !craftComplete))
      {
        loadCraft(file);
        showLoadWindow = false;
        showHistory = null;
      }
    }

    private bool createCraftInfo(string file, string craftFileName, DateTime craftEditTime, int craftPartCount, int craftStages, float craftCost, bool craftComplete, bool hideDate = false)
    {
      GUILayout.BeginVertical();
      GUILayout.Space(8);
      string uniqueID = file + craftPartCount + craftStages + craftCost + craftComplete + hideDate;
      if (!toggleCategories.ContainsKey(uniqueID))
        toggleCategories.Add(uniqueID, false);
      GUILayout.BeginHorizontal();
      if (GUILayout.Toggle(toggleCategories[uniqueID], new GUIContent("", "Show/hide extended info"), arrowStyle))
      {
        toggleCategories[uniqueID] = true;
      }
      else
      {
        toggleCategories[uniqueID] = false;
      }
      Utilities.createLabel(craftFileName, craftNameStyle);
      GUILayout.EndHorizontal();
      if (toggleCategories[uniqueID])
      {
        if (!hideDate)
          Utilities.createLabel(craftEditTime.ToString("yyyy.MM.dd HH:mm:ss"), craftStyle);

        Utilities.createLabel(getPartAndStageString(craftPartCount, "Part", false) + " in " + getPartAndStageString(craftStages, "Stage"), craftStyle);
        Utilities.createLabel("Craft cost: " + craftCost.ToString("N0"), craftStyle);
        if (!craftComplete)
          Utilities.createLabel("Craft is missing Parts", textStyleRed);
      }
      GUILayout.EndVertical();
      return toggleCategories[uniqueID];
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
        var craftFileName = filesDic[file].Item1;
        var craftEditTime = filesDic[file].Item2;
        var craftPartCount = filesDic[file].Item3;
        var craftStages = filesDic[file].Item4;
        var craftCost = filesDic[file].Item5;
        var craftComplete = filesDic[file].Item6;
        if (!craftComplete && currentSettings.getBool("hideUnloadableCrafts"))
          continue;
        GUILayout.BeginVertical(areaStyle);
        GUILayout.BeginHorizontal();
        double craftTime = 0;
        double.TryParse(craftFileName, out craftTime);
        craftFileName = Utilities.convertUnixTimestampToDate(craftTime).ToString("yyyy.MM.dd HH:mm:ss");
        bool show = createCraftInfo(file, craftFileName, craftEditTime, craftPartCount, craftStages, craftCost, craftComplete, true);
        GUILayout.BeginVertical();
        createCraftLoadButton(file, craftComplete);
        if (show)
          createCraftDeleteButton(file);
        GUILayout.Space(0);
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
        showHistory = null;
      }
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
      GUI.DragWindow();
    }

    #endregion ui
  }
}