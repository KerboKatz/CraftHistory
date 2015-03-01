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
    private bool categoriesModified;
    private bool catsSetAlready;
    private bool exInfoSetAlready;
    private bool hideUnloadableCrafts;
    private bool historyOnDemand;
    private bool initStyle                                                                                            = false;
    private bool saveAll;
    private bool saveInInterval;
    private bool saveWorkerCompleted                                                                                  = true;
    private bool showLoadWindow;
    private bool toggleAllCats;
    private bool toggleAllExtendedInfo;
    private bool windowCenterd                                                                                        = false;
    private bool workerCompleted                                                                                      = true;
    private Dictionary<double, int> partCount                                                                         = new Dictionary<double, int>();
    private Dictionary<string, bool> toggleCategories                                                                 = new Dictionary<string, bool>();
    private Dictionary<string, bool> toggleExtendedInfo                                                               = new Dictionary<string, bool>();
    private Dictionary<string, List<KeyValuePair<string, string>>> categories                                         = new Dictionary<string, List<KeyValuePair<string, string>>>();
    private Dictionary<string, string[]> historyFilesDic                                                              = new Dictionary<string, string[]>();
    private Dictionary<string, Tuple<string, DateTime, int, int, float, bool, string[], bool, bool>> filesDicToUpdate = new Dictionary<string, Tuple<string, DateTime, int, int, float, bool, string[], bool, bool>>();
    private Dictionary<string, Tuple<string, DateTime, int, int, float, bool>> filesDic                               = new Dictionary<string, Tuple<string, DateTime, int, int, float, bool>>();
    private double nextCheck                                                                                          = 0;
    private float tooltipHeight                                                                                       = 0;
    private GUIStyle addCatLoadIconStyle;
    private GUIStyle areaStyle;
    private GUIStyle arrowStyle;
    private GUIStyle buttonDeleteIconStyle;
    private GUIStyle buttonHistoryIconStyle;
    private GUIStyle buttonLoadIconStyle;
    private GUIStyle buttonStyle;
    private GUIStyle categoryEditTextStyle;
    private GUIStyle categoryStyle;
    private GUIStyle categoryTextStyle;
    private GUIStyle containerStyle;
    private GUIStyle craftNameStyle;
    private GUIStyle craftStyle;
    private GUIStyle editCatAddStyle;
    private GUIStyle editCategoriesWindowStyle;
    private GUIStyle labelStyle;
    private GUIStyle loadWindowStyle;
    private GUIStyle numberFieldStyle;
    private GUIStyle searchFieldStyle;
    private GUIStyle searchTextStyle;
    private GUIStyle settingsWindowStyle;
    private GUIStyle showHideAllTextStyle;
    private GUIStyle textStyle;
    private GUIStyle textStyleRed;
    private GUIStyle toggleStyle;
    private GUIStyle tooltipStyle;
    private int editCategoriesWindowID                                                                                = 20248491;
    private int historyWindowID                                                                                       = 844526732;
    private int loadCraftID                                                                                           = 56706112;
    private int settingsWindowID                                                                                      = 971199;
    private List<Exception> exceptions                                                                                = new List<Exception>();
    private List<string> currentCraftCategories                                                                       = new List<string>();
    private List<string> historyFilesAddedToDic                                                                       = new List<string>();
    private List<string> toBeRemoved                                                                                  = new List<string>();
    private List<Tuple<ConfigNode, string, double, string>> requestedBackups                                          = new List<Tuple<ConfigNode, string, double, string>>();
    private Rect editCategoriesWindow                                                                                 = new Rect(0, 0, 350, 505);
    private Rect historyWindow;
    private Rect loadWindowPosition                                                                                   = new Rect(0, 0, 350, 505);
    private Rect settingsWindow                                                                                       = new Rect(0, 0, 230, 225);
    private Rect tooltipRect                                                                                          = new Rect(0, 0, 230, 20);
    private settings currentSettings;
    private string addToCategoryString                                                                                = "";
    private string CurrentTooltip;
    private string delimiter;
    private string historyFiles;
    private string modName                                                                                            = "CraftHistory";
    private string saveInterval;
    private string searchCraft                                                                                        = "";
    private string showHistory;
    private Vector2 catWindowScrollPosition                                                                           = new Vector2();
    private Vector2 loadWindowScrollPosition                                                                          = new Vector2();
    private Vector2 scrollPositionHistory;
    private Version requiresUtilities                                                                                 = new Version(1, 0, 3);
    private GUIStyle arrowBrightStyle;

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
      currentSettings        = new settings();
      currentSettings.load(modName, "settings", modName);
      currentSettings.setDefault("saveAll", "false");
      currentSettings.setDefault("saveInInterval", "false");
      currentSettings.setDefault("historyOnDemand", "false");
      currentSettings.setDefault("saveInterval", "1");
      currentSettings.setDefault("hideUnloadableCrafts", "True");
      currentSettings.setDefault("delimiter", ";");
      currentSettings.setDefault("showEditCategories", "false");
      currentSettings.set("editorScene", getEditorScene());
      hideUnloadableCrafts   = currentSettings.getBool("hideUnloadableCrafts");
      saveAll                = currentSettings.getBool("saveAll");
      saveInterval           = currentSettings.getString("saveInterval");
      delimiter              = currentSettings.getString("delimiter");
      historyOnDemand        = currentSettings.getBool("historyOnDemand");
      saveInInterval         = currentSettings.getBool("saveInInterval");
      changePathTo(currentSettings.getString("editorScene"), true);
      if (currentSettings.isSet("editCategoriesWindowX"))
      {
        editCategoriesWindow.x = currentSettings.getFloat("editCategoriesWindowX");
        editCategoriesWindow.y = currentSettings.getFloat("editCategoriesWindowY");
      }
      else
      {
        editCategoriesWindow.x = Screen.width;
        editCategoriesWindow.y = Screen.height - editCategoriesWindow.height - 38;
      }

      if (!windowCenterd && loadWindowPosition.x == 0 && loadWindowPosition.y == 0 && loadWindowPosition.width > 0 && loadWindowPosition.height > 0)
      {
        loadWindowPosition.x = Screen.width / 2 - loadWindowPosition.width / 2;
        loadWindowPosition.y = Screen.height / 2 - loadWindowPosition.height / 2;
        settingsWindow.x     = currentSettings.getFloat("windowX");
        settingsWindow.y     = currentSettings.getFloat("windowY");
        if (settingsWindow.x == 0 && settingsWindow.y == 0)
        {
          settingsWindow.x = Screen.width;
          settingsWindow.y = Screen.height - settingsWindow.height - 38;
        }
        windowCenterd = true;
      }
      categories.Add("VAB", new List<KeyValuePair<string, string>>());
      categories.Add("SPH", new List<KeyValuePair<string, string>>());
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
        EditorLogic.fetch.loadBtn.methodToInvoke           = "toggleWindow";
        EditorLogic.fetch.loadBtn.scriptWithMethodToInvoke = this;
      }
      if (EditorLogic.fetch.saveBtn.methodToInvoke != "saveCraft")
      {
        EditorLogic.fetch.saveBtn.methodToInvoke           = "saveCraft";
        EditorLogic.fetch.saveBtn.scriptWithMethodToInvoke = this;
      }
      changePathTo(getEditorScene(), true);
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
        currentSettings.set("editCategoriesWindowX", editCategoriesWindow.x);
        currentSettings.set("editCategoriesWindowY", editCategoriesWindow.y);
        currentSettings.save();
      }
      GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
      GameEvents.onEditorShipModified.Remove(onCraftChange);
      if (button != null)
      {
        ApplicationLauncher.Instance.RemoveModApplication(button);
      }
      while (requestedBackups.Count > 0 && exceptions.Count == 0)
      {//if for some reason the game gets ended before all crafts are saved save them before destroying
        //check for exceptions too that could cause the game to freeze while shuting down and at the end to crash
        if (workerCompleted)
        {
          backupCraft(false);
        }
      }
    }

    private string getSavePath()
    {
      return "saves/" + HighLogic.SaveFolder + "/Ships/" + getEditorScene() + "/";
    }

    private void changePathTo(string mode, bool dontUpdateCraftList = false)
    {
      currentSettings.set("editorScene", mode);
      currentSettings.set("savePath", "saves/" + HighLogic.SaveFolder + "/Ships/" + mode + "/");
      if (!dontUpdateCraftList)
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
      if ((currentSettings.getBool("saveAll") || currentSettings.getBool("saveInInterval")) &&
          Utilities.getUnixTimestamp() > nextCheck &&
          requestedBackups.Count > 0 &&
          workerCompleted)
      {
        updateNextCheck();
        backupCraft();
      }
      if (categoriesModified)
      {
        sortCategories();
        categoriesModified = false;
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

    private bool backupCraft(bool useMultiThread = true)
    {
      try
      {
        int i = 0;
        if (!currentSettings.getBool("saveAll") && currentSettings.getBool("saveInInterval"))
        {
          i = requestedBackups.Count - 1;
        }
        workerCompleted = false;
        if (useMultiThread)
        {
          ThreadPool.QueueUserWorkItem(new WaitCallback(backgrounder), new object[] { requestedBackups[i], i });
        }
        else
        {
          backgrounder(new object[] { requestedBackups[i], i });
        }
      }
      catch (Exception e)
      {
        Debug.LogException(e);
      }
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
      if (
          !currentSettings.getBool("saveAll") && !currentSettings.getBool("saveInInterval"))
        return;
      if (craft.Parts.Count <= 0)
        return;
      if (!File.Exists(getSavePath() + craft.shipName + ".craft"))
        return;
      var saveCraft = craft.SaveShip();
      foreach (string currentCat in currentCraftCategories)
      {
        if (currentCat.IsNullOrWhiteSpace())
          continue;
        saveCraft.AddValue("category", currentCat);
      }
      var newTuple = new Tuple<ConfigNode, string, double, string>(saveCraft, craft.shipName, Utilities.getUnixTimestamp(), getSavePath());
      if (!requestedBackups.Contains(newTuple))
        requestedBackups.Add(newTuple);
      return;
    }

    private void saveCraft(ConfigNode savedCraft, string craftName, double timestamp, string savePath)
    {
      savePath = savePath + craftName;
      var saveFile = savePath + "/" + timestamp + ".craft";
      Directory.CreateDirectory(savePath);
      if (!File.Exists(saveFile))
        savedCraft.Save(saveFile);
    }

    private void saveCraft()
    {
      ThreadPool.QueueUserWorkItem(new WaitCallback(saveCraft), new object[] { EditorLogic.fetch.ship, currentCraftCategories, currentSettings.getString("historyOnDemand"), getSavePath(), EditorLogic.fetch.shipNameField.Text });
    }

    private void saveCraft(object state)
    {
      try
      {
        if (!saveWorkerCompleted)
        {
          while (!saveWorkerCompleted)
          {
            Thread.Sleep(100);
          }
        }
        saveWorkerCompleted = false;
        object[] args       = state as object[];
        var shipConstruct   = args[0] as ShipConstruct;
        var currentCats     = args[1] as List<string>;
        var savePath        = args[3] as string;
        var historyOnDemand = args[2] as string;
        var currentCraft    = shipConstruct.SaveShip();
        var shipName        = args[4] as string;//shipConstruct.shipName;
        currentCraft.SetValue("ship", shipName);
        foreach (string currentCat in currentCats)
        {
          if (currentCat.IsNullOrWhiteSpace())
            continue;
          currentCraft.AddValue("category", currentCat);
        }
        var saveFile = savePath + shipName + ".craft";
        currentCraft.Save(saveFile);
        if (historyOnDemand == "True")
        {
          savePath = savePath + shipName;
          var saveFileH = savePath + "/" + Utilities.getUnixTimestamp() + ".craft";
          Directory.CreateDirectory(savePath);
          if (!File.Exists(saveFileH))
            File.Copy(saveFile, saveFileH);
          updateHistoryFilesDicContents(saveFile);
        }
        saveWorkerCompleted = true;
        addToFilesDic(saveFile, false, true);
      }
      catch (Exception e)
      {
        exceptions.Add(e);
        saveWorkerCompleted = true;
      }
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
      if (Input.GetMouseButtonUp(0))
      {//left mouse button
        if (currentSettings.getBool("showEditCategories"))
        {
          currentSettings.set("showEditCategories", false);
        }
        else
        {
          currentSettings.set("showEditCategories", true);
        }
      }
      else if (Input.GetMouseButtonUp(1))//right mouse button
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
    }

    private void loadCraft(string craftFile)
    {
      EditorLogic.LoadShipFromFile(craftFile);
    }

    private void updateCraftList()
    {
      var filesArray = getFiles(currentSettings.getString("savePath"));
      foreach (string file in filesArray)
      {
        addToFilesDic(file);
        updateHistoryFilesDicContents(file);
      }
    }

    private void updateHistoryFilesDicContents(string file)
    {
      if (!historyFilesDic.ContainsKey(file))
        historyFilesDic.Add(file, Utilities.reverseArray(getFiles(file.Replace(".craft", "") + "/")));
      else
        historyFilesDic[file] = Utilities.reverseArray(getFiles(file.Replace(".craft", "") + "/"));
    }

    private string[] getFiles(string path)
    {
      if (Directory.Exists(path))
        return Directory.GetFiles(path, "*.craft", SearchOption.TopDirectoryOnly);
      else
        return new string[0];
    }

    private void addToFilesDic(string file, bool isHistoryFile = false, bool overwriteExisting = false)
    {
      if ((!filesDic.ContainsKey(file)) || overwriteExisting)
      {
        ThreadPool.QueueUserWorkItem(new WaitCallback(requestExtendedInfo), new object[] { file, isHistoryFile.ToString(), overwriteExisting.ToString() });
      }
    }

    private void requestExtendedInfo(object state)
    {
      try
      {
        object[] args             = state as object[];
        string file               = args[0] as string;
        string isHistoryFileS     = args[1] as string;
        string overwriteExistingS = args[2] as string;
        bool isHistoryFile;
        if (isHistoryFileS        == "True")
        {
          isHistoryFile = true;
        }
        else
        {
          isHistoryFile = false;
        }
        bool overwriteExisting;
        if (overwriteExistingS == "True")
        {
          overwriteExisting = true;
        }
        else
        {
          overwriteExisting = false;
        }
        if ((filesDic.ContainsKey(file) && !overwriteExisting) || filesDicToUpdate.ContainsKey(file))
          return;
        int partCount = 0, stageCount = 0;
        float craftCost = 0;
        bool craftComplete = true;
        string[] craftCategories;
        FileInfo fileInfo = new FileInfo(file);
        getCraftInfo(file, out partCount, out stageCount, out craftCost, out craftComplete, out craftCategories);
        filesDicToUpdate.Add(file, new Tuple<string, DateTime, int, int, float, bool, string[], bool, bool>(
          fileInfo.Name.Replace(".craft", ""),
          fileInfo.LastWriteTime,
          partCount,
          stageCount,
          craftCost,
          craftComplete,
          craftCategories,
          isHistoryFile,
          overwriteExisting
          ));
      }
      catch (Exception e)
      {
        exceptions.Add(e);
      }
    }

    private void sortCategories()
    {
      foreach (var cats in categories)
      {
        cats.Value.Sort((x, y) =>
        {
          var xKey = String.IsNullOrEmpty(x.Key);
          var yKey = String.IsNullOrEmpty(y.Key);
          if (xKey && !yKey)
          {
            return 1;
          }
          else if (!xKey && yKey)
          {
            return -1;
          }
          else if (xKey && yKey)
          {
            return x.Value.CompareTo(y.Value);
          }
          else
          {
            return x.Key.CompareTo(y.Key);
          }
        });
      }
      categoriesModified = false;
    }

    private bool setCategoryFromName(string file, bool addedToCategories)
    {
      if (!string.IsNullOrEmpty(currentSettings.getString("delimiter")))
      {
        string catergory = "";
        var thisCategory = filesDic[file].Item1.Split(currentSettings.getString("delimiter").ToCharArray(), 2, StringSplitOptions.None);
        if (thisCategory[0] != filesDic[file].Item1)
        {
          catergory = thisCategory[0];
          addedToCategories = true;
          categories[getCraftTypeByFilePath(file)].AddUnique(new KeyValuePair<string, string>(catergory, file));
        }
      }
      return addedToCategories;
    }

    private bool setCategories(string file, bool addedToCategories, string[] craftCategories)
    {
      foreach (string catergory in craftCategories)
      {
        addedToCategories = true;
        categories[getCraftTypeByFilePath(file)].AddUnique(new KeyValuePair<string, string>(catergory, file));
      }
      return addedToCategories;
    }

    private static void getCraftInfo(string file, out int partCount, out int stageCount, out float craftCost, out bool craftComplete, out string[] craftCategories)
    {
      var nodes = ConfigNode.Load(file);
      var partNodes = nodes.GetNodes("PART");
      partCount = partNodes.Length;
      Utilities.getCraftCostAndStages(nodes, partNodes, out stageCount, out craftCost, out craftComplete, out craftCategories);
    }

    private string getCraftTypeByFilePath(string path)
    {
      if (path.Contains("/VAB/"))
        return "VAB";
      else
        return "SPH";
    }

    private void updateFilesOnRepaint()
    {
      if (filesDicToUpdate.Count > 0)
      {
        var toRemove = new List<string>();
        var count = filesDicToUpdate.Count;
        foreach (var cur in filesDicToUpdate.Keys)
        {
          if (filesDicToUpdate[cur].Item9)
          {
            removeCraftEntry(cur);
          }
          if (!filesDic.ContainsKey(cur))
          {
            filesDic.Add(cur, new Tuple<string, DateTime, int, int, float, bool>(
              filesDicToUpdate[cur].Item1,
              filesDicToUpdate[cur].Item2,
              filesDicToUpdate[cur].Item3,
              filesDicToUpdate[cur].Item4,
              filesDicToUpdate[cur].Item5,
              filesDicToUpdate[cur].Item6));
            if (!filesDicToUpdate[cur].Item8)
            {
              var addedToCategories = false;
              addedToCategories = setCategories(cur, addedToCategories, filesDicToUpdate[cur].Item7);
              addedToCategories = setCategoryFromName(cur, addedToCategories);
              if (!addedToCategories)
                categories[getCraftTypeByFilePath(cur)].AddUnique(new KeyValuePair<string, string>("", cur));
              categoriesModified = true;//sortCategories();
            }
          }
          toRemove.Add(cur);
        }
        foreach (var file in toRemove)
        {
          filesDicToUpdate.Remove(file);
        }
      }
      foreach (var file in toBeRemoved)
      {
        removeCraftEntry(file);
      }
      toBeRemoved.Clear();
    }

    private void removeCraftEntry(string file)
    {
      filesDic.Remove(file);
      foreach (var categoryPath in categories)
      {
        var toBeRemovedFromCats = new List<KeyValuePair<string, string>>();
        foreach (var curCat in categoryPath.Value)
        {
          if (curCat.Value == file)
            toBeRemovedFromCats.Add(curCat);
        }
        foreach (var remove in toBeRemovedFromCats)
        {
          categoryPath.Value.Remove(remove);
        }
      }
      categoriesModified = true;
    }

    #region ui
    private void OnGUI()
    {
      if (!initStyle)
        InitStyle();
      if (HighLogic.LoadedSceneIsEditor)
      {
        if (Event.current.type == EventType.Layout)
        {
          updateFilesOnRepaint();
        }
        createWindow(currentSettings.getBool("showSettings"), settingsWindowID, ref settingsWindow, createSettingsWindow, "CraftHistory Settings", settingsWindowStyle);
        createWindow(currentSettings.getBool("showEditCategories"), editCategoriesWindowID, ref editCategoriesWindow, editCategories, "Edit craft categories", editCategoriesWindowStyle);
        createWindow(showLoadWindow, loadCraftID, ref loadWindowPosition, loadWindow, "Select a craft to load", loadWindowStyle);
        createWindow(!String.IsNullOrEmpty(showHistory), historyWindowID, ref historyWindow, createHistoryWindow, "Select a craft from history to load", loadWindowStyle);

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

    private void createWindow(bool showWindow, int windowID, ref Rect windowRect, GUI.WindowFunction windowFunction, string windowName, GUIStyle windowStyle)
    {
      if (showWindow)
      {
        windowRect = GUILayout.Window(windowID, windowRect, windowFunction, windowName, windowStyle);
        Utilities.clampToScreen(ref windowRect);
        Utilities.lockEditor(windowRect, windowID.ToString());
      }
      else
      {
        EditorLogic.fetch.Unlock(windowID.ToString());
      }
    }

    private void InitStyle()
    {
      labelStyle              = new GUIStyle(HighLogic.Skin.label);
      labelStyle.stretchWidth = true;

      loadWindowStyle              = new GUIStyle(HighLogic.Skin.window);
      loadWindowStyle.fixedWidth   = 350;
      loadWindowStyle.padding.left = 0;
      loadWindowStyle.fixedHeight  = 505;

      settingsWindowStyle              = new GUIStyle(HighLogic.Skin.window);
      settingsWindowStyle.fixedWidth   = 250;
      settingsWindowStyle.padding.left = 0;

      editCategoriesWindowStyle              = new GUIStyle(HighLogic.Skin.window);
      editCategoriesWindowStyle.fixedWidth   = 350;
      editCategoriesWindowStyle.fixedHeight  = 505;
      editCategoriesWindowStyle.padding.left = 0;

      toggleStyle                  = new GUIStyle(HighLogic.Skin.toggle);
      toggleStyle.normal.textColor = labelStyle.normal.textColor;
      toggleStyle.active.textColor = labelStyle.normal.textColor;

      arrowStyle                     = new GUIStyle(toggleStyle);
      arrowStyle.active.background   = Utilities.getTexture("CraftHistoryToggle_off_hover", "CraftHistory/Textures");
      arrowStyle.onActive.background = Utilities.getTexture("CraftHistoryToggle_on_hover", "CraftHistory/Textures");
      arrowStyle.normal.background   = Utilities.getTexture("CraftHistoryToggle_off", "CraftHistory/Textures");
      arrowStyle.onNormal.background = Utilities.getTexture("CraftHistoryToggle_on", "CraftHistory/Textures");
      arrowStyle.hover.background    = Utilities.getTexture("CraftHistoryToggle_off_hover", "CraftHistory/Textures");
      arrowStyle.onHover.background  = Utilities.getTexture("CraftHistoryToggle_on_hover", "CraftHistory/Textures");

      arrowStyle.fixedHeight     = 20;
      arrowStyle.fixedWidth      = 20;
      arrowStyle.overflow.bottom = 0;
      arrowStyle.overflow.top    = 0;
      arrowStyle.overflow.left   = 0;
      arrowStyle.overflow.right  = 0;
      arrowStyle.padding.top     = 0;
      arrowStyle.padding.left    = 0;
      arrowStyle.padding.right   = 0;
      arrowStyle.padding.bottom  = 0;

      arrowBrightStyle                     = new GUIStyle(arrowStyle);
      arrowBrightStyle.active.background   = Utilities.getTexture("CraftHistoryToggle_bottom_off_hover", "CraftHistory/Textures");
      arrowBrightStyle.onActive.background = Utilities.getTexture("CraftHistoryToggle_bottom_on_hover", "CraftHistory/Textures");
      arrowBrightStyle.normal.background   = Utilities.getTexture("CraftHistoryToggle_bottom_off", "CraftHistory/Textures");
      arrowBrightStyle.onNormal.background = Utilities.getTexture("CraftHistoryToggle_bottom_on", "CraftHistory/Textures");
      arrowBrightStyle.hover.background    = Utilities.getTexture("CraftHistoryToggle_bottom_off_hover", "CraftHistory/Textures");
      arrowBrightStyle.onHover.background  = Utilities.getTexture("CraftHistoryToggle_bottom_on_hover", "CraftHistory/Textures");

      textStyle             = new GUIStyle(HighLogic.Skin.label);
      textStyle.fixedWidth  = 150;
      textStyle.margin.left = 10;

      textStyleRed                  = new GUIStyle(textStyle);
      textStyleRed.normal.textColor = Color.red;

      searchTextStyle             = new GUIStyle(textStyle);
      searchTextStyle.margin.top  = 4;
      searchTextStyle.padding.top = 0;
      searchTextStyle.fixedWidth  = 50;

      showHideAllTextStyle            = new GUIStyle(searchTextStyle);
      showHideAllTextStyle.fixedWidth = 85;
      showHideAllTextStyle.margin.top = 2;

      craftStyle             = new GUIStyle(HighLogic.Skin.label);
      craftStyle.fixedWidth  = 260;
      craftStyle.margin.left = 10;

      craftNameStyle             = new GUIStyle(craftStyle);
      craftNameStyle.fixedWidth  = 240;
      craftNameStyle.margin.left = 5;
      craftNameStyle.margin.top  = 0;
      craftNameStyle.fontStyle   = FontStyle.Bold;

      categoryTextStyle                 = new GUIStyle(craftStyle);
      categoryTextStyle.fontStyle       = FontStyle.Bold;
      categoryTextStyle.padding.top     = 0;
      categoryTextStyle.margin.top      = 2;
      categoryEditTextStyle             = new GUIStyle(categoryTextStyle);
      categoryEditTextStyle.padding.top = 5;

      editCatAddStyle             = new GUIStyle(HighLogic.Skin.box);
      editCatAddStyle.fixedHeight = 30;

      containerStyle             = new GUIStyle(GUI.skin.button);
      containerStyle.fixedWidth  = 230;
      containerStyle.margin.left = 10;

      numberFieldStyle               = new GUIStyle(HighLogic.Skin.box);
      numberFieldStyle.fixedWidth    = 52;
      numberFieldStyle.fixedHeight   = 22;
      numberFieldStyle.alignment     = TextAnchor.MiddleCenter;
      numberFieldStyle.margin.left   = 45;
      numberFieldStyle.padding.right = 7;
      numberFieldStyle.margin.top    = 4;

      searchFieldStyle             = new GUIStyle(HighLogic.Skin.box);
      searchFieldStyle.fixedHeight = 22;

      buttonDeleteIconStyle                   = new GUIStyle(GUI.skin.button);
      buttonDeleteIconStyle.fixedWidth        = 30;
      buttonDeleteIconStyle.fixedHeight       = 30;
      buttonDeleteIconStyle.margin.top        = 0;
      buttonDeleteIconStyle.normal.background = Utilities.getTexture("button_Delete", "CraftHistory/Textures");
      buttonDeleteIconStyle.hover.background  = Utilities.getTexture("button_Delete_mouseover", "CraftHistory/Textures");
      buttonDeleteIconStyle.active            = buttonDeleteIconStyle.hover;

      buttonHistoryIconStyle                   = new GUIStyle(buttonDeleteIconStyle);
      buttonHistoryIconStyle.normal.background = Utilities.getTexture("button_History", "CraftHistory/Textures");
      buttonHistoryIconStyle.hover.background  = Utilities.getTexture("button_History_mouseover", "CraftHistory/Textures");
      buttonHistoryIconStyle.active            = buttonHistoryIconStyle.hover;

      buttonLoadIconStyle                   = new GUIStyle(buttonDeleteIconStyle);
      buttonLoadIconStyle.normal.background = Utilities.getTexture("button_Load", "CraftHistory/Textures");
      buttonLoadIconStyle.hover.background  = Utilities.getTexture("button_Load_mouseover", "CraftHistory/Textures");
      buttonLoadIconStyle.active            = buttonLoadIconStyle.hover;

      addCatLoadIconStyle                   = new GUIStyle(buttonDeleteIconStyle);
      addCatLoadIconStyle.normal.background = Utilities.getTexture("button_Save", "CraftHistory/Textures");
      addCatLoadIconStyle.hover.background  = Utilities.getTexture("button_Save_mouseover", "CraftHistory/Textures");
      addCatLoadIconStyle.active            = buttonLoadIconStyle.hover;

      buttonStyle            = new GUIStyle(GUI.skin.button);
      buttonStyle.fixedWidth = 100;
      buttonStyle.alignment  = TextAnchor.MiddleCenter;

      categoryStyle            = new GUIStyle(HighLogic.Skin.button);
      categoryStyle.fixedWidth = 330;
      categoryStyle.onHover    = categoryStyle.normal;
      categoryStyle.hover      = categoryStyle.normal;

      areaStyle            = new GUIStyle(HighLogic.Skin.button);
      areaStyle.fixedWidth = 320;
      areaStyle.onHover    = areaStyle.normal;
      areaStyle.hover      = areaStyle.normal;

      tooltipStyle                   = new GUIStyle(HighLogic.Skin.label);
      tooltipStyle.fixedWidth        = 230;
      tooltipStyle.padding.top       = 5;
      tooltipStyle.padding.left      = 5;
      tooltipStyle.padding.right     = 5;
      tooltipStyle.padding.bottom    = 5;
      tooltipStyle.fontSize          = 10;
      tooltipStyle.normal.background = Utilities.getTexture("tooltipBG", "Textures");
      tooltipStyle.normal.textColor  = Color.white;
      tooltipStyle.border.top        = 1;
      tooltipStyle.border.bottom     = 1;
      tooltipStyle.border.left       = 8;
      tooltipStyle.border.right      = 8;
      tooltipStyle.stretchHeight     = true;

      initStyle = true;
    }

    private void editCategories(int id)
    {
      List<string> toBeRemoved = new List<string>();
      GUILayout.BeginVertical();
      catWindowScrollPosition = GUILayout.BeginScrollView(catWindowScrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(350), GUILayout.Height(420));//420
      foreach (string cat in currentCraftCategories)
      {
        if (cat.IsNullOrWhiteSpace())
          continue;
        GUILayout.BeginVertical(areaStyle);
        GUILayout.BeginHorizontal();
        Utilities.createLabel(cat, categoryEditTextStyle);
        if (Utilities.createButton("", buttonDeleteIconStyle, "Once you finished editing categories you have to save your craft to apply the changes!"))
        {
          toBeRemoved.Add(cat);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
      }
      GUILayout.EndScrollView();
      GUILayout.FlexibleSpace();
      createAddToCategoryButton();
      GUILayout.EndVertical();
      GUI.DragWindow();
      foreach (var rem in toBeRemoved)
      {
        currentCraftCategories.Remove(rem);
      }
    }

    private void createAddToCategoryButton()
    {
      GUILayout.BeginHorizontal();
      Utilities.createLabel("Add to category:", textStyle);
      addToCategoryString = GUILayout.TextField(addToCategoryString, int.MaxValue, editCatAddStyle);
      if (Utilities.createButton("", addCatLoadIconStyle, "Once you finished editing categories you have to save your craft to apply the changes!"))
      {
        currentCraftCategories.AddUnique(addToCategoryString);
      }
      GUILayout.EndHorizontal();
    }

    private void createSettingsWindow(int id)
    {
      GUILayout.BeginVertical();

      if (GUILayout.Toggle(historyOnDemand, new GUIContent("History on demand", "Enable to create a history point during saving of the craft"), toggleStyle))
      {
        historyOnDemand = true;
        saveAll         = false;
        saveInInterval  = false;
      }
      else
      {
        historyOnDemand = false;
      }
      if (GUILayout.Toggle(saveAll, new GUIContent("Save every iteration of the craft", "Enable to save every change of the craft"), toggleStyle))
      {
        saveAll         = true;
        historyOnDemand = false;
        saveInInterval  = false;
      }
      else
      {
        saveAll = false;
      }
      if (GUILayout.Toggle(saveInInterval, new GUIContent("Save in interval", "Enable to only save the latest iteration of the craft after X seconds"), toggleStyle))
      {
        saveInInterval  = true;
        historyOnDemand = false;
        saveAll         = false;
      }
      else
      {
        saveInInterval = false;
      }
      GUILayout.BeginHorizontal();
      GUI.enabled = saveInInterval;
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
        currentSettings.set("saveAll", saveAll);
        currentSettings.set("saveInInterval", saveInInterval);
        currentSettings.set("hideUnloadableCrafts", hideUnloadableCrafts);
        currentSettings.set("saveInterval", saveInterval);
        currentSettings.set("delimiter", delimiter);
        currentSettings.set("historyOnDemand", historyOnDemand);
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
      GUILayout.BeginHorizontal();
      Utilities.createLabel("Search:", searchTextStyle);
      searchCraft = GUILayout.TextField(searchCraft, int.MaxValue, searchFieldStyle);
      GUILayout.EndHorizontal();
      loadWindowScrollPosition = GUILayout.BeginScrollView(loadWindowScrollPosition, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(350), GUILayout.Height(400));//420
      var currentCategory = "";
      var startedCategory = false;
      foreach (KeyValuePair<string, string> pair in categories[getCraftTypeByFilePath(currentSettings.getString("savePath"))])
      {
        if (!searchCraft.IsNullOrWhiteSpace() && !pair.Value.Contains(searchCraft, StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }
        var file = pair.Value;
        if (!filesDic.ContainsKey(file))
        {
          continue;
        }
        var category       = pair.Key;
        var craftFileName  = filesDic[file].Item1;
        var craftEditTime  = filesDic[file].Item2;
        var craftPartCount = filesDic[file].Item3;
        var craftStages    = filesDic[file].Item4;
        var craftCost      = filesDic[file].Item5;
        var craftComplete  = filesDic[file].Item6;
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
            if (!historyFilesAddedToDic.Contains(file))
            {
              foreach (string hFile in historyFilesDic[file])
              {
                addToFilesDic(hFile, true);
                historyFilesAddedToDic.Add(hFile);
              }
            }
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

      createToggleAllButtons();
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

    private void createToggleAllButtons()
    {
      GUILayout.BeginVertical();
      GUILayout.Space(5);
      GUILayout.BeginHorizontal();
      Utilities.createLabel("Show/hide all:", showHideAllTextStyle);
      if (GUILayout.Toggle(toggleAllCats, new GUIContent("", "Categories"), arrowBrightStyle))
      {
        toggleAllCats = true;
        setAllCategoriesTo(true);
      }
      else
      {
        setAllCategoriesTo(false);
        toggleAllCats = false;
      }
      if (GUILayout.Toggle(toggleAllExtendedInfo, new GUIContent("", "Extended craft info"), arrowBrightStyle))
      {
        toggleAllExtendedInfo = true;
        setAllExInfoTo(true);
      }
      else
      {
        setAllExInfoTo(false);
        toggleAllExtendedInfo = false;
      }
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
    }

    private void setAllExInfoTo(bool setTo)
    {
      if (exInfoSetAlready != setTo)
      {
        var keys = new List<string>(toggleExtendedInfo.Keys);
        foreach (var curExInfo in keys)
        {
          toggleExtendedInfo[curExInfo] = setTo;
        }
        exInfoSetAlready = setTo;
      }
    }

    private void setAllCategoriesTo(bool setTo)
    {
      if (catsSetAlready != setTo)
      {
        foreach (var categoryPath in categories)
        {
          foreach (var curCat in categoryPath.Value)
          {
            toggleCategories[curCat.Key] = setTo;
          }
        }
        catsSetAlready = setTo;
      }
    }

    private void createCraftDeleteButton(string file, string historyPath = null)
    {
      if (Utilities.createButton("", buttonDeleteIconStyle))
      {
        if (File.Exists(file))
          File.Delete(file);
        toBeRemoved.AddUnique(file);
        deleteHistory(historyPath, file);
      }
    }

    private void deleteHistory(string historyPath, string file)
    {
      if (!string.IsNullOrEmpty(historyPath))
      {
        if (showHistory == historyPath)
          showHistory = null;
        if (Directory.Exists(historyPath))
          Directory.Delete(historyPath, true);
        if (historyFilesDic.ContainsKey(file))
          foreach (string hFile in historyFilesDic[file])
          {
            toBeRemoved.AddUnique(hFile);
          }
      }
      updateCraftList();
    }

    private void createCraftLoadButton(string file, bool craftComplete)
    {
      if (Utilities.createButton("", buttonLoadIconStyle, !craftComplete))
      {
        currentCraftCategories.Clear();
        foreach (var categoryPath in categories)
        {
          foreach (KeyValuePair<string, string> pair in categoryPath.Value)
          {
            if (pair.Value == file)
            {
              currentCraftCategories.Add(pair.Key);
            }
          }
        }
        if (File.Exists(file))
          loadCraft(file);
        showLoadWindow = false;
        showHistory = null;
      }
    }

    private bool createCraftInfo(string file, string craftFileName, DateTime craftEditTime, int craftPartCount, int craftStages, float craftCost, bool craftComplete, bool hideDate = false)
    {
      GUILayout.BeginVertical();
      //GUILayout.Space(8);
      GUILayout.Space(4);
      if (!toggleExtendedInfo.ContainsKey(file))
        toggleExtendedInfo.Add(file, false);
      GUILayout.BeginHorizontal();
      if (GUILayout.Toggle(toggleExtendedInfo[file], new GUIContent("", "Show/hide extended info"), arrowStyle))
      {
        toggleExtendedInfo[file] = true;
      }
      else
      {
        toggleExtendedInfo[file] = false;
      }
      Utilities.createLabel(craftFileName, craftNameStyle);
      GUILayout.EndHorizontal();
      if (toggleExtendedInfo[file])
      {
        if (!hideDate)
          Utilities.createLabel(craftEditTime.ToString("yyyy.MM.dd HH:mm:ss"), craftStyle);

        Utilities.createLabel(Utilities.getPartAndStageString(craftPartCount, "Part", false) + " in " + Utilities.getPartAndStageString(craftStages, "Stage"), craftStyle);
        Utilities.createLabel("Craft cost: " + craftCost.ToString("N0"), craftStyle);
        if (!craftComplete)
          Utilities.createLabel("Craft is missing Parts", textStyleRed);
      }
      GUILayout.EndVertical();
      return toggleExtendedInfo[file];
    }

    private void createHistoryWindow(int windowID)
    {
      GUILayout.BeginVertical();

      scrollPositionHistory = GUILayout.BeginScrollView(scrollPositionHistory, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(350), GUILayout.Height(445));
      foreach (string file in historyFilesDic[historyFiles])
      {
        if (!filesDic.ContainsKey(file))
        {
          continue;
        }
        var craftFileName  = filesDic[file].Item1;
        var craftEditTime  = filesDic[file].Item2;
        var craftPartCount = filesDic[file].Item3;
        var craftStages    = filesDic[file].Item4;
        var craftCost      = filesDic[file].Item5;
        var craftComplete  = filesDic[file].Item6;
        if (!craftComplete && currentSettings.getBool("hideUnloadableCrafts"))
          continue;
        GUILayout.BeginVertical(areaStyle);
        GUILayout.BeginHorizontal();
        double craftTime   = 0;
        double.TryParse(craftFileName, out craftTime);
        craftFileName      = Utilities.convertUnixTimestampToDate(craftTime).ToString("yyyy.MM.dd HH:mm:ss");
        bool show          = createCraftInfo(file, craftFileName, craftEditTime, craftPartCount, craftStages, craftCost, craftComplete, true);
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
        deleteHistory(showHistory, historyFiles);
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