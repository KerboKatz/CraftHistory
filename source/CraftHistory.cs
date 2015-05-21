using KerboKatz.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace KerboKatz
{
  [KSPAddon(KSPAddon.Startup.EditorAny, false)]
  public partial class CraftHistory : KerboKatzBase
  {
    private bool saveWorkerCompleted = true;
    private bool workerCompleted = true;
    private Dictionary<double, int> partCount = new Dictionary<double, int>();
    private Dictionary<string, List<KeyValuePair<string, string>>> categories = new Dictionary<string, List<KeyValuePair<string, string>>>();
    private Dictionary<string, string[]> historyFilesDic = new Dictionary<string, string[]>();
    private Dictionary<string, craftObject> filesDicToUpdate = new Dictionary<string, craftObject>();
    private Dictionary<string, craftObject> filesDic = new Dictionary<string, craftObject>();
    private double nextCheck = 0;
    private List<Exception> exceptions = new List<Exception>();
    private List<List<string>> sortOptions = new List<List<string>>();
    private List<string> currentCraftCategories = new List<string>();
    private List<string> existingCraftCategories = new List<string>();
    private List<string> historyFilesAddedToDic = new List<string>();
    private List<string> toBeRemoved = new List<string>();
    private List<Tuple<ConfigNode, string, double, string>> requestedBackups = new List<Tuple<ConfigNode, string, double, string>>();
    private string existingCraftCategoriesFile = "";

    public CraftHistory()
    {
      modName = "CraftHistory";
      tooltip = "Use left click to show the current crafts categories.\n Use right click to open the settings menu.";
      requiresUtilities = new Version(1, 2, 0);
    }

    protected override void Awake()
    {
      base.Awake();
      GameEvents.onEditorShipModified.Add(onCraftChange);
      thisButton.onClick = onToolbar;
    }

    protected override void Started()
    {
      sortOptions.Add(new List<string> { "Name ▲", "Price ▲", "Stages ▲", "Part Count ▲", "Last edit ▲" });
      sortOptions.Add(new List<string> { "Name ▼", "Price ▼", "Stages ▼", "Part Count ▼", "Last edit ▼" });
      //DontDestroyOnLoad(this);
      currentSettings.setDefault("saveAll", "false");
      currentSettings.setDefault("saveInInterval", "false");
      currentSettings.setDefault("historyOnDemand", "false");
      currentSettings.setDefault("saveInterval", "1");
      currentSettings.setDefault("hideUnloadableCrafts", "True");
      currentSettings.setDefault("delimiter", ";");
      currentSettings.setDefault("showEditCategories", "false");
      currentSettings.setDefault("sortOption", "0");
      currentSettings.setDefault("sortOrder", "0");
      currentSettings.setDefault("deleteThumbnails", "True");

      currentSettings.set("editorScene", getEditorScene());

      hideUnloadableCrafts = currentSettings.getBool("hideUnloadableCrafts");
      saveAll = currentSettings.getBool("saveAll");
      saveInterval = currentSettings.getString("saveInterval");
      delimiter = currentSettings.getString("delimiter");
      historyOnDemand = currentSettings.getBool("historyOnDemand");
      saveInInterval = currentSettings.getBool("saveInInterval");
      sortOption = currentSettings.getInt("sortOption");
      sortOrder = currentSettings.getInt("sortOrder");
      deleteThumbnails = currentSettings.getBool("deleteThumbnails");

      editExistingCraftCategoriesWindow.x = currentSettings.getFloat("editExistingCraftCategoriesWindowX");
      editExistingCraftCategoriesWindow.y = currentSettings.getFloat("editExistingCraftCategoriesWindowY");

      changePathTo(currentSettings.getString("editorScene"), true);
      if (currentSettings.isSet("editCategoriesWindowX"))
      {
        editCategoriesWindow.x = currentSettings.getFloat("editCategoriesWindowX");
        editCategoriesWindow.y = currentSettings.getFloat("editCategoriesWindowY");
      }
      categories.Add("VAB", new List<KeyValuePair<string, string>>());
      categories.Add("SPH", new List<KeyValuePair<string, string>>());

      setIcon(Utilities.getTexture("icon", "CraftHistory/Textures"));
      setAppLauncherScenes(ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB);

      if (EditorLogic.fetch.loadBtn.methodToInvoke != "toggleWindow")
      {
        EditorLogic.fetch.loadBtn.Invoke(EditorLogic.fetch.loadBtn.methodToInvoke, 0);
        EditorLogic.fetch.loadBtn.methodToInvoke = "toggleWindow";
        EditorLogic.fetch.loadBtn.scriptWithMethodToInvoke = this;
      }
      if (EditorLogic.fetch.saveBtn.methodToInvoke != "saveCraft")
      {
        EditorLogic.fetch.saveBtn.methodToInvoke = "saveCraft";
        EditorLogic.fetch.saveBtn.scriptWithMethodToInvoke = this;
      }
      if (EditorLogic.fetch.exitBtn.methodToInvoke != "exitBtn")
      {
        EditorLogic.fetch.exitBtn.methodToInvoke = "exitBtn";
        EditorLogic.fetch.exitBtn.scriptWithMethodToInvoke = this;
      }
      changePathTo(getEditorScene(), true);
    }

    public void exitBtn()
    {
      GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
      HighLogic.LoadScene(GameScenes.SPACECENTER);
    }

    protected override void OnDestroy()
    {
      Utilities.debug(modName, "destroy");
      if (currentSettings != null)
      {
        currentSettings.set("showSettings", false);
        currentSettings.set("showSettings", false);
        currentSettings.set("windowX", settingsWindow.x);
        currentSettings.set("windowY", settingsWindow.y);
        currentSettings.set("editCategoriesWindowX", editCategoriesWindow.x);
        currentSettings.set("editCategoriesWindowY", editCategoriesWindow.y);
        currentSettings.set("editExistingCraftCategoriesWindowX", editExistingCraftCategoriesWindow.x);
        currentSettings.set("editExistingCraftCategoriesWindowY", editExistingCraftCategoriesWindow.y);
      }
      GameEvents.onEditorShipModified.Remove(onCraftChange);
      while (requestedBackups.Count > 0 && exceptions.Count == 0)
      {//if for some reason the game gets ended before all crafts are saved save them before destroying
        //check for exceptions too that could cause the game to freeze while shuting down and at the end to crash
        if (workerCompleted)
        {
          backupCraft(false);
        }
      }
      base.OnDestroy();
    }

    private string getSavePath()
    {
      return "saves/" + HighLogic.SaveFolder + "/Ships/" + getEditorScene() + "/";
    }

    private void changePathTo(string mode, bool dontUpdateCraftList = false)
    {
      currentEditor = mode;
      currentSettings.set("editorScene", mode);
      currentSettings.set("savePath", "saves/" + HighLogic.SaveFolder + "/Ships/" + mode + "/");
      if (!dontUpdateCraftList)
        updateCraftList();
      showHistory = null;
    }

    private void FixedUpdate()
    {
      if ((currentSettings.getBool("saveAll") || currentSettings.getBool("saveInInterval")) &&
          Time.time > nextCheck &&
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
        nextCheck = Time.time + 1;
      }
      else
      {
        nextCheck = Time.time + currentSettings.getDouble("saveInterval");
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
      if (currentSettings == null)
        return;
      if (!currentSettings.getBool("saveAll") && !currentSettings.getBool("saveInInterval"))
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
      //var fileName = getEditorScene() + "_" + EditorLogic.fetch.ship.shipName;
      //ShipConstruction.CaptureThumbnail(EditorLogic.fetch.ship, "/saves/" + HighLogic.SaveFolder + "/Ships/@thumbs/", fileName);
      var fileName = HighLogic.SaveFolder + "_" + getEditorScene() + "_" + EditorLogic.fetch.ship.shipName;
      ShipConstruction.CaptureThumbnail(EditorLogic.fetch.ship, "/thumbs/", fileName);
      GetThumbnail(fileName, true);
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
        ConfigNode currentCraft;
        object[] args = state as object[];
        var shipConstruct = args[0] as ShipConstruct;
        var currentCats = args[1] as List<string>;
        var savePath = args[3] as string;
        var historyOnDemand = args[2] as string;
        if (shipConstruct != null)
          currentCraft = shipConstruct.SaveShip();
        else
          currentCraft = ConfigNode.Load(args[0] as string);
        var shipName = args[4] as string;//shipConstruct.shipName;
        currentCraft.SetValue("ship", shipName);
        currentCraft.RemoveValues("category");
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

    protected override void onToolbar()
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
        object[] args = state as object[];
        string file = args[0] as string;
        string isHistoryFileS = args[1] as string;
        string overwriteExistingS = args[2] as string;
        bool isHistoryFile;
        if (isHistoryFileS == "True")
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
        filesDicToUpdate.Add(file, new craftObject(
          fileInfo.Name.Replace(".craft", ""),//1
          fileInfo.LastWriteTime,//2
          partCount,//3
          stageCount,//4
          craftCost,//5
          craftComplete,//6
          craftCategories,//7
          isHistoryFile,//8
          overwriteExisting//9
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
          else if (!xKey && !yKey || !xKey && !yKey && yKey != xKey)
          {
            return x.Key.CompareTo(y.Key);
          }
          else
          {
            if (filesDic.ContainsKey(x.Value) && filesDic.ContainsKey(y.Value))
            {
              var sortOption = currentSettings.getInt("sortOption");//{ "Name", "Price", "Stages", "Part Count","Last edit"}
              int returnV;
              if (currentSettings.getInt("sortOrder") == 1)
              {
                returnV = choseSortOption(y.Value, x.Value, sortOption);
              }
              else
              {
                returnV = choseSortOption(x.Value, y.Value, sortOption);
              }
              return returnV;
            }
            return x.Value.CompareTo(y.Value);
          }
        });
      }
      categoriesModified = false;
    }

    private int choseSortOption(string x, string y, int sortOption)
    {
      if (sortOption == 1)
      {
        return sortByPrice(x, y);
      }
      else if (sortOption == 2)
      {
        return sortByStages(x, y);
      }
      else if (sortOption == 3)
      {
        return sortByPartCount(x, y);
      }
      else if (sortOption == 4)
      {
        return sortByLastEdit(x, y);
      }
      else
      {
        return sortByName(x, y);
      }
    }

    private int sortByPartCount(string x, string y)
    {
      if (filesDic[x].partCount != filesDic[y].partCount)
        return filesDic[x].partCount.CompareTo(filesDic[y].partCount);
      else
        return sortByName(x, y);
    }

    private int sortByPrice(string x, string y)
    {
      if (filesDic[x].craftCost != filesDic[y].craftCost)
        return filesDic[x].craftCost.CompareTo(filesDic[y].craftCost);
      else
        return sortByName(x, y);
    }

    private int sortByStages(string x, string y)
    {
      if (filesDic[x].stageCount != filesDic[y].stageCount)
        return filesDic[x].stageCount.CompareTo(filesDic[y].stageCount);
      else
        return sortByName(x, y);
    }

    private int sortByLastEdit(string x, string y)
    {
      if (filesDic[x].lastEdit != filesDic[y].lastEdit)
        return filesDic[x].lastEdit.CompareTo(filesDic[y].lastEdit);
      else
        return sortByName(x, y);
    }

    private int sortByName(string x, string y)
    {
      return filesDic[x].craftName.CompareTo(filesDic[y].craftName);
    }

    private bool setCategoryFromName(string file, bool addedToCategories)
    {
      if (!string.IsNullOrEmpty(currentSettings.getString("delimiter")))
      {
        string catergory = "";
        var thisCategory = filesDic[file].craftName.Split(currentSettings.getString("delimiter").ToCharArray(), 2, StringSplitOptions.None);
        if (thisCategory[0] != filesDic[file].craftName)
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
      Utilities.Craft.getCraftCostAndStages(nodes, partNodes, out stageCount, out craftCost, out craftComplete, out craftCategories);
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
          if (filesDicToUpdate[cur].overwriteExisting)
          {
            removeCraftEntry(cur);
          }
          if (!filesDic.ContainsKey(cur))
          {
            filesDic.Add(cur, new craftObject(
              filesDicToUpdate[cur].craftName,
              filesDicToUpdate[cur].lastEdit,
              filesDicToUpdate[cur].partCount,
              filesDicToUpdate[cur].stageCount,
              filesDicToUpdate[cur].craftCost,
              filesDicToUpdate[cur].craftComplete));
            if (!filesDicToUpdate[cur].isHistoryFile)
            {
              var addedToCategories = false;
              addedToCategories = setCategories(cur, addedToCategories, filesDicToUpdate[cur].craftCategories);
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
      if (toBeRemoved.Count > 0)
      {
        foreach (var file in toBeRemoved)
        {
          removeCraftEntry(file);
        }
        toBeRemoved.Clear();
      }
      if (categoriesModified)
      {
        sortCategories();
        categoriesModified = false;
      }
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
  }
}