using KerboKatz.Assets;
using KerboKatz.Extensions;
using KerboKatz.Toolbar;
using KerboKatz.UI;
using KerboKatzUtilities.WorkDispatcher;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KerboKatz.CH
{
  [KSPAddon(KSPAddon.Startup.EditorAny, false)]
  public class CraftHistory : KerboKatzBase<Settings>, IToolbar
  {
    private const string historyDirectory = "_Backup";
    private List<GameScenes> _activeScences = new List<GameScenes>() { GameScenes.EDITOR };
    private CraftData[] craftData = new CraftData[0];
    private Transform template;
    private Transform viewportContent;
    private UIData loadCraftsWindow;
    private string activePath;
    private string applicationRootPath;
    private string currentLoadedCraft;

    //private UIData currentCategoriesPrefab;
    //private UIData currentCategoriesWindow;
    private CraftData currentCraftData = new CraftData();

    private Transform categoryTemplate;
    private HashSet<string> categories = new HashSet<string>();
    private DirectoryInfo currentDirectory;

    //private string initialDirectory;
    private double lastLoadTime;

    private List<CraftDirectoryData> directoryInfos = new List<CraftDirectoryData>();
    private string lastTimeStamp;
    private Texture2D stockTexture;
    private Dictionary<string, Texture2D> thumbCache = new Dictionary<string, Texture2D>();
    private string mainSavePath;

    //private string _savePath;
    private bool isHistoryDirectory;

    private bool activePathIsDirectory;

    //private Dictionary<string, InputField> directoryInputField = new Dictionary<string, InputField>();
    private UIData settingsWindow;

    private Transform foldersViewportContent;
    private UIData deleteWindow;
    private Transform searchTemplate;
    private List<CraftData> searchResults = new List<CraftData>();
    private Pool<GameObject> searchObjectPool;
    private bool backupIsQueued;
    private Button historyReturnButton;
    private InputField searchInput;
    private bool uiUpdateDone;
    private CraftData copyCraftData;
    private UIData modalWindow;
    private Image bigThumbnailImage;

    //private Pool<CraftData> craftDataPool;

    public CraftHistory()
    {
      modName = "CraftHistory";
      displayName = "CraftHistory";
      tooltip = "Use left click to show the current crafts categories.\n Use right click to open the settings menu.";
      requiresUtilities = new Version(1, 3, 8);
      ToolbarBase.instance.Add(this);
      Log("Init done!");
    }

    public override void OnAwake()
    {
      applicationRootPath = KSPUtil.ApplicationRootPath;
      LoadSettings("CraftHistory", "Settings");
      settings.debug = true;
      LoadUI("CraftHistory", "CraftHistory/CraftHistory");
      LoadUI("CraftHistorySettings", "CraftHistory/CraftHistory");
      LoadUI("CraftHistoryDeleteConfirmation", "CraftHistory/CraftHistory");
      LoadUI("CraftHistoryModalWindow", "CraftHistory/CraftHistory");
      //LoadUI("CraftHistoryActiveCraftCategories", "CraftHistory/CraftHistory");

      GameEvents.onEditorShipModified.Add(OnShipModified);
      GameEvents.onEditorRestart.Add(OnEditorRestart);//fired when New Craft is pressed!
      //have to remove the default listener but since there is no way to do this we have to remove all listeners
      //this will interfere with other mods so i am open for suggestions.
      //one way to do this would be to use the KerboKatzUtilities to remove the stock one from there and add let the mods listen to a callback from there.
      ButtonEventReplacer.Add(EditorLogic.fetch.loadBtn, ToggleSavedCraftFiles, true);
      ButtonEventReplacer.Add(EditorLogic.fetch.saveBtn, SaveShip, true);
      ButtonEventReplacer.Add(EditorLogic.fetch.exitBtn, ExitEditor, true);
      ButtonEventReplacer.Add(EditorLogic.fetch.newBtn, NewShip, true);

      stockTexture = Instantiate(AssetBase.GetTexture("craftThumbGeneric"));
      mainSavePath = Path.Combine(applicationRootPath, "saves");
      mainSavePath = Path.Combine(mainSavePath, HighLogic.SaveFolder);
      mainSavePath = Path.Combine(mainSavePath, "Ships");

      mainSavePath = Path.GetFullPath(mainSavePath);
      OnEditorRestart();

      InitSearchObjectPool();
    }


    private void InitSearchObjectPool()
    {
      searchObjectPool = new Pool<GameObject>();
      searchObjectPool.Generator = () =>
      {
        return Instantiate(searchTemplate.gameObject);
      };
      searchObjectPool.Reseter = (item) =>
      {
        item.transform.SetParent(null, false);
        item.SetActive(false);
      };
    }

    private void NewShip()
    {
      //need something better here but no idea what right now...
      EditorDriver.StartupBehaviour = EditorDriver.StartupBehaviours.START_CLEAN;
      EditorDriver.StartEditor(EditorDriver.editorFacility);
    }
    private void ExitEditor()
    {
      GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
      HighLogic.LoadScene(GameScenes.SPACECENTER);
    }

    private void OnEditorRestart()
    {
      currentCraftData = new CraftData();
      SetIsHistoryDirectory(false);
      currentCraftData.directoryInfo = currentDirectory;
    }

    private void SetIsHistoryDirectory(bool status)
    {
      isHistoryDirectory = status;
      if (historyReturnButton != null)
        historyReturnButton.gameObject.SetActive(status);
      if (searchInput != null)
        searchInput.interactable = !status;
    }

    protected override void BeforeSaveOnDestroy()
    {
      if (loadCraftsWindow != null)
      {
        loadCraftsWindow.gameObject.SetActive(false);
      }
      GameEvents.onEditorRestart.Remove(OnEditorRestart);
      ToolbarBase.instance.Remove(this);
    }

    private void ToggleSavedCraftFiles()
    {
      settings.showLoadCrafts = !settings.showLoadCrafts;
      if (settings.showLoadCrafts)
      {
        OnIsVABChange(IsVAB());
        GetCraftFiles();
        FadeCanvasGroup(loadCraftsWindow.canvasGroup, 1, settings.uiFadeSpeed);
      }
      else
      {
        FadeCanvasGroup(loadCraftsWindow.canvasGroup, 0, settings.uiFadeSpeed);
      }
    }

    #region saving the craft

    private void OnShipModified(ShipConstruct data)
    {
      if (settings.backupInterval == BackupInterval.Disabled)
        return;
      if (settings.backupInterval == BackupInterval.OnSave)
        return;
      if (currentCraftData.fileInfo == null)
      {
        Log("CraftData is unknown. Ship isn't saved ?");
        return;
      }
      if (currentCraftData.fileInfo.Name == "Auto-Saved Ship.craft")
      {
        Log("Save file is auto-saved!");
        return;
      }
      StartCoroutine(CreateCraftBackup(data));
    }

    private IEnumerator CreateCraftBackup(ShipConstruct data)
    {
      if (backupIsQueued)
        yield break;
      backupIsQueued = true;
      var shipName = GetShipName();
      if (currentCraftData.name != shipName)
      {
        Log("Ship name missmatch ", currentCraftData.name, "<==>", currentCraftData.name);
        backupIsQueued = false;
        yield break;
      }
      if (settings.backupInterval == BackupInterval.Timed)
      {
        yield return new WaitForSeconds(settings.backupDelay);
      }
      var currentTimeStamp = DateTime.Now.ToString("dd.MM.yy_HH.mm.ss.fff");

      var path = Directory.CreateDirectory(Path.Combine(currentCraftData.directoryInfo.FullName, GetBackupDirectoryName(currentCraftData.path)));
      Log("Creating backup at ", path.FullName);
      data.SaveShip().Save(Path.Combine(path.FullName, currentTimeStamp + ".craft"));
      backupIsQueued = false;
    }

    private DirectoryInfo GetCurrentSaveDirectory()
    {
      if (isHistoryDirectory)
        return currentDirectory.Parent;
      return currentDirectory;
    }

    private void SaveShip()
    {
      //update ship name and data we don't want this to be outdated or we would get confused!
      EditorLogic.fetch.ship.shipName = EditorLogic.fetch.shipNameField.text;
      EditorLogic.fetch.ship.shipDescription = EditorLogic.fetch.shipDescriptionField.text;

      currentCraftData.name = EditorLogic.fetch.ship.shipName;
      currentCraftData.path = Path.Combine(GetCurrentSaveDirectory().FullName, GetShipName()) + ".craft";

      //save a thumbnail
      ShipConstruction.CaptureThumbnail(EditorLogic.fetch.ship, "thumbs", GetThumbnailName(currentCraftData.name, currentCraftData.path));

      currentCraftData.configNode = EditorLogic.fetch.ship.SaveShip();

      Log("Saving craft to: ", currentCraftData.path);
      currentCraftData.configNode.Save(currentCraftData.path);
      currentCraftData.fileInfo = new FileInfo(currentCraftData.path);
      currentCraftData.directoryInfo = currentCraftData.fileInfo.Directory;
      if (settings.backupInterval == BackupInterval.OnSave)
        StartCoroutine(CreateCraftBackup(EditorLogic.fetch.ship));
    }

    private static string GetShipName()
    {
      return KSPUtil.SanitizeString(EditorLogic.fetch.ship.shipName, ' ', false);
    }

    private static bool IsVAB()
    {
      if (EditorDriver.editorFacility == EditorFacility.SPH)
        return false;
      return true;
    }

    #endregion saving the craft

    #region ui

    protected override void OnUIElemntInit(UIData uiWindow)
    {
      var prefabWindow = uiWindow.gameObject.transform as RectTransform;
      switch (uiWindow.name)
      {
        case "CraftHistory":
          loadCraftsWindow = uiWindow;
          var content = prefabWindow.FindChild("Content");
          var scrollView = content.FindChild("Scroll View");
          var viewport = scrollView.FindChild("Viewport");
          viewportContent = viewport.FindChild("Content");
          template = viewportContent.FindChild("Template");
          template.SetParent(prefabWindow);
          template.gameObject.SetActive(false);
          searchTemplate = viewportContent.FindChild("SearchTemplate");
          searchTemplate.SetParent(prefabWindow);
          searchTemplate.gameObject.SetActive(false);

          var folders = content.FindChild("Folders");
          var foldersScrollView = folders.FindChild("Scroll View");
          var foldersViewport = foldersScrollView.FindChild("Viewport");
          foldersViewportContent = foldersViewport.FindChild("Content");

          categoryTemplate = foldersViewportContent.FindChild("CategoryTemplate");
          categoryTemplate.SetParent(prefabWindow);
          categoryTemplate.gameObject.SetActive(false);

          var isVAB = IsVAB();
          var sph_vab = content.FindChild("VAB_SPH");
          var vabToggle = InitToggle(sph_vab, "VAB", isVAB, OnIsVABChange);
          var sphToggle = InitToggle(sph_vab, "SPH", !isVAB);
          var options = content.FindChild("Options");

          InitButton(options, "Load", LoadCraft);
          InitButton(options, "Merge", MergeCraft);
          InitButton(options, "Delete", DeleteActiveSelection);
          InitButton(options, "ShowHistory", ShowHistory);
          InitButton(options, "RenameParent", Rename);
          InitButton(options, "NewFolderParent", CreateNewDirectory);
          InitButton(prefabWindow, "Cancel", ToggleSavedCraftFiles);
          historyReturnButton = InitButton(content, "Return", OnHistoryReturn);
          if (!isHistoryDirectory)
            historyReturnButton.gameObject.SetActive(false);
          searchInput = InitInputField(content, "SearchBar", "");
          searchInput.onValueChange.AddListener(FilterCraft);

          bigThumbnailImage = GetComponentInChild<Image>(content, "BigThumbnail");

          FadeCanvasGroup(loadCraftsWindow.canvasGroup, 0, settings.uiFadeSpeed);
          OnUIScaleChange(settings.uiScale);
          break;

        case "CraftHistorySettings":
          settingsWindow = uiWindow;

          InitButton(prefabWindow, "Cancel", OnToolbar);
          InitToggle(prefabWindow, "OnSaveOption", settings.backupInterval == BackupInterval.OnSave, (isOn) => { if (isOn) OnBackupInterval(BackupInterval.OnSave); });
          InitToggle(prefabWindow, "OnChangeOption", settings.backupInterval == BackupInterval.OnChange, (isOn) => { if (isOn) OnBackupInterval(BackupInterval.OnChange); });
          InitToggle(prefabWindow, "IntervalOption", settings.backupInterval == BackupInterval.Timed, (isOn) => { if (isOn) OnBackupInterval(BackupInterval.Timed); });
          InitToggle(prefabWindow, "disable", settings.backupInterval == BackupInterval.Disabled, (isOn) => { if (isOn) OnBackupInterval(BackupInterval.Disabled); });
          InitInputField(prefabWindow, "IntervalOption", settings.backupDelay.ToString(), OnBackupDelayChange);
          InitToggle(prefabWindow, "HideUnloadableOption", settings.hideUnloadable, OnHideUnloadableChange);
          InitDropdown(prefabWindow, "SortOption", OnSortOptionChange, (int)settings.sortOption);
          InitToggle(prefabWindow, "SortOption", settings.ascending, OnAscendingChange);
          InitSlider(prefabWindow, "UIscale", settings.uiScale, OnUIScaleChange);
          break;

        case "CraftHistoryDeleteConfirmation":
          deleteWindow = uiWindow;
          DestroyOldUIWindow(deleteWindow);
          break;

        case "CraftHistoryModalWindow":
          modalWindow = uiWindow;
          DestroyOldUIWindow(modalWindow);
          break;
      }
    }

    private void OnUIScaleChange(float arg0)
    {
      if (settings.uiScale != arg0)
      {
        settings.uiScale = arg0;
        SaveSettings();
      }
      var scale = arg0 / 100;
      loadCraftsWindow.gameObject.transform.localScale = new Vector3(scale, scale, scale);
    }

    private void OnHistoryReturn()
    {
      currentDirectory = currentDirectory.Parent;
      SetIsHistoryDirectory(false);
      GetCraftFiles();
    }

    private void OnIsVABChange(bool arg0)
    {
      Log("OnIsVABChange");
      settings.isVAB = arg0;
      currentDirectory = new DirectoryInfo(GetFilesPath());
      GetCraftFiles();
      RefreshDirectories();
    }

    private void RefreshDirectories()
    {
      directoryInfos.Clear();
      DeleteChildren(foldersViewportContent);
      //directoryInputField.Clear();
      /*if (currentDirectory.Parent.FullName != mainSavePath)
      {
        GenerateDirctoryObject(currentDirectory.Parent, "..");
      }*/
      CreateSubDirectories(currentDirectory.Parent, new DirectoryInfo[] { currentDirectory });
    }

    private bool CreateSubDirectories(DirectoryInfo currentDir, DirectoryInfo[] children, int level = 0)
    {
      //var children = currentDir.GetDirectories();
      Array.Sort(children, CompareDirectoryInfo);
      foreach (var directory in children)
      {
        var skip = false;
        foreach (var file in currentDir.GetFiles("*.craft", SearchOption.TopDirectoryOnly))
        {
          if (Path.GetFileNameWithoutExtension(file.Name) + historyDirectory == directory.Name)
          {
            skip = true;
            break;
          }
        }
        if (skip)
          continue;
        var directoryChildren = directory.GetDirectories();
        GenerateDirectoryObject(directory, directoryChildren.Length > 0, level);
        CreateSubDirectories(directory, directoryChildren, level + 1);
      }
      return (children.Length > 0);
    }

    private static int CompareDirectoryInfo(DirectoryInfo lhs, DirectoryInfo rhs)
    {
      return Utilities.Compare(lhs.Name, rhs.Name);
    }

    private void GenerateDirectoryObject(DirectoryInfo directory, bool hasChildren = false, int level = 0)
    {
      var data = new CraftDirectoryData();
      directoryInfos.Add(data);
      var parentData = GetDirectoryData(directory.Parent.FullName);
      if (parentData != null)
        parentData.children.Add(data);
      if (data.name.IsNullOrWhiteSpace())
        data.name = directory.Name;//Path.GetFullPath(directory);
      var newCategoryOption = Instantiate(categoryTemplate.gameObject);
      data.gameObject = newCategoryOption;
      UpdateirectoryGameObjectName(data);
      newCategoryOption.transform.SetParent(foldersViewportContent, false);
      newCategoryOption.SetActive(true);
      newCategoryOption.GetComponent<DoubleClick>().onDoubleClick.AddListener(() =>
      {
        OpenDirectory(data);
      });
      newCategoryOption.GetComponent<Toggle>().onValueChanged.AddListener((status) =>
      {
        if (!status)
          return;
        SetActiveDirectory(data);
      });
      //InitTextField(newCategoryOption.transform, "Text", VisualName);
      var nameInputField = InitInputField(newCategoryOption.transform, "Name", data.name);
      SetInputInteractable(nameInputField, false);
      nameInputField.onEndEdit.AddListener((newName) =>
      {
        SetInputInteractable(nameInputField, false);
        if (data.name == "..")
        {
          nameInputField.text = data.name;
          return;
        }
        RenameDirectory(data, newName);
      });
      //directoryInfos.Add(directory.FullName, nameInputField);
      newCategoryOption.GetComponent<Drop>().onObjectDroped.AddListener((source) =>
      {
        OnDropCraft(data, source);
      });
      data.path = directory.FullName;
      data.input = nameInputField;
      data.uiObjectTransform = newCategoryOption.transform;
      data.directoryInfo = directory;
      var expanding = newCategoryOption.GetComponent<ExpandingInputField>();
      expanding.paddingLeft.x += 7 * level;
      var toggle = InitToggle(newCategoryOption.transform, "Name", true);
      if (hasChildren)
      {
        toggle.onValueChanged.AddListener(data.ToggleChildren);
      }
      else
      {
        toggle.gameObject.SetActive(false);
      }
      data.contextMenu = data.gameObject.GetComponent<UI.ContextMenu>();
      data.contextMenu.Init();
      data.contextMenuPaste = data.contextMenu.AddOption("Paste");
      data.contextMenuPaste.button.onClick.AddListener(() =>
      {
        if (copyCraftData == null)
          return;
        if (!craftData.Contains(copyCraftData))
          return;
        CopyCraftTo(data);
      });
    }

    private void CopyCraftTo(CraftDirectoryData data, bool force = false)
    {
      Log("Paste");
      var pastePath = Path.Combine(data.directoryInfo.FullName, copyCraftData.fileInfo.Name);
      if (!force)
      {
        if (File.Exists(pastePath))
        {
          CreateModalWindow(modalWindow.prefab, "Overwrite file?", "File already exists at target location. Do you want to overwrite it ?", () => { CopyCraftTo(data, true); }, null);
          return;
        }
      }
      var oldThumbnail = GetThumbnailPath(GetThumbnailName(copyCraftData.name, copyCraftData.path));
      var newThumbnail = GetThumbnailPath(GetThumbnailName(copyCraftData.name, pastePath));
      if (File.Exists(newThumbnail))
      {
        Log("Thumbnail already exists at: ", pastePath, ". Deleting...");
        File.Delete(newThumbnail);
      }
      if (File.Exists(oldThumbnail))
      {
        Log(oldThumbnail, " to ", newThumbnail);
        File.Copy(oldThumbnail, newThumbnail, true);
      }
      var backupDirectory = GetBackupDirectory(pastePath);
      if (Directory.Exists(backupDirectory))
        Directory.Delete(backupDirectory, true);
      copyCraftData.fileInfo.CopyTo(pastePath, force);
    }

    private static void CreateModalWindow(GameObject prefab, string title, string description, UnityAction yesAction, UnityAction noAction)
    {
      //DestroyOldUIWindow(deleteWindow);
      //InstantiateUIWindow(deleteWindow);
      var newModalWindow = Instantiate(prefab);
      newModalWindow.transform.SetParent(CanvasController.instance.canvas.transform, false);
      newModalWindow.SetActive(true);
      newModalWindow.transform.localPosition = Vector3.zero;
      var controller = newModalWindow.GetComponent<ModalWindowController>();
      controller.title.text = title;
      controller.mainText.text = description;
      if (yesAction != null)
        controller.confirm.onClick.AddListener(yesAction);
      if (noAction != null)
        controller.deny.onClick.AddListener(noAction);
    }

    private void SetActiveDirectory(CraftDirectoryData data)
    {
      activePathIsDirectory = true;
      activePath = data.directoryInfo.FullName;
    }

    private static void UpdateirectoryGameObjectName(CraftDirectoryData data)
    {
      data.gameObject.name = "1 - Category - " + data.name;
    }

    private void OpenDirectory(CraftDirectoryData currentCrafDirectory)
    {
      ClearSearchResult();
      currentDirectory = currentCrafDirectory.directoryInfo;
      SetIsHistoryDirectory(false);
      GetCraftFiles();
    }

    private void RenameDirectory(CraftDirectoryData directory, string newName)
    {
      if (directory.directoryInfo.Name == newName)
      {
        Log("Old name and new name matches");
        return;
      }
      Log("Old name: ", directory.directoryInfo.Name, " setting to ", newName);
      Log("Old directory at ", directory.directoryInfo.FullName);

      var newPath = Path.Combine(directory.directoryInfo.Parent.FullName, KSPUtil.SanitizeString(newName, ' ', false));
      Log("New file at ", newPath, " Saving....");

      if (Directory.Exists(newPath))
      {
        Log("Target directory already exists!");
        return;
      }
      directory.directoryInfo.MoveTo(newPath);
      directory.directoryInfo = new DirectoryInfo(newPath);
      directory.name = newName;
      //Log(directory.Exists,"____",directory.Name);
      UpdateirectoryGameObjectName(directory);
      SortCraftsAndFolders();
    }

    private void OnHideUnloadableChange(bool arg0)
    {
      Log("OnHideUnloadableChange");
      settings.hideUnloadable = arg0;
      SaveSettings();
    }

    private void OnBackupDelayChange(string arg0)
    {
      Log("OnBackupDelayChange");
      settings.backupDelay = arg0.ToFloat();
      SaveSettings();
    }

    private void DeleteActiveSelection()
    {
      Log("DeleteActiveSelection");
      if (activePath.IsNullOrWhiteSpace())
      {
        Log("No craft selected!");
        return;
      }
      DestroyOldUIWindow(deleteWindow);
      InstantiateUIWindow(deleteWindow);
      deleteWindow.gameObject.SetActive(true);
      deleteWindow.gameObject.transform.localPosition = Vector3.zero;
      if (!activePathIsDirectory)
      {
        var renameCraftData = GetActiveCraftData();
        InitButton(deleteWindow.gameObject.transform, "Yup", () => { DeleteActiveSelectionConfirm(renameCraftData); });
      }
      else
      {
        CraftDirectoryData renameDirectoryData = GetActiveDirectoryData();
        InitButton(deleteWindow.gameObject.transform, "Yup", () => { DeleteActiveSelectionConfirm(renameDirectoryData); });
      }
      InitButton(deleteWindow.gameObject.transform, "Nope", () => { DestroyOldUIWindow(deleteWindow); });
      InitButton(deleteWindow.gameObject.transform, "Yup", () => { DestroyOldUIWindow(deleteWindow); });
    }

    private void DeleteActiveSelectionConfirm(CraftData data)
    {
      data.fileInfo.Delete();
      var backupDirectory = GetBackupDirectory(data.path);
      if (Directory.Exists(backupDirectory))
        Directory.Delete(backupDirectory, true);
      var thumbnailPath = GetThumbnailPath(GetThumbnailName(data.name, data.path));
      if (File.Exists(thumbnailPath))
        File.Delete(thumbnailPath);
      Destroy(data.gameObject);
    }

    private void DeleteActiveSelectionConfirm(CraftDirectoryData data)
    {
      data.directoryInfo.Delete(true);
      Destroy(data.gameObject);
    }

    private void FilterDirectory(string arg0)
    {
      Log("Searching for: ", arg0);
      foreach (var data in directoryInfos)
      {
        if (data.directoryInfo.Name.Contains(arg0, StringComparison.OrdinalIgnoreCase))
        {
          Log(data.directoryInfo.Name, " contains ", arg0);
          data.gameObject.SetActive(true);
        }
        else
        {
          Log(data.directoryInfo.Name, " doesn't contain ", arg0);
          data.gameObject.SetActive(false);
        }
      }
    }

    private void FilterCraft(string arg0)
    {
      var sw = new Stopwatch();
      sw.Start();

      ClearSearchResult();
      if (!uiUpdateDone)
        return;
      Log(sw.Elapsed.TotalMilliseconds, " Searching for: ", arg0);
      var isNullOrWhiteSpace = arg0.IsNullOrWhiteSpace();
      if (!isNullOrWhiteSpace)
      {
        foreach (var path in (new DirectoryInfo(mainSavePath)).GetFiles("*" + arg0 + "*.craft", SearchOption.AllDirectories))
        {
          if (path.FullName.Contains(historyDirectory))
          {
            continue;
          }
          var data = new CraftData();
          data.path = path.FullName;
          data.name = Path.GetFileNameWithoutExtension(path.FullName);
          data.fileInfo = path;

          data.directoryInfo = path.Directory;
          CreateSearchCraftOption(data);
          searchResults.Add(data);
        }
        SortCraftsAndFolders();
      }
      Log("Search took: ", sw.Elapsed.TotalMilliseconds, " ms");
      foreach (var data in craftData)
      {
        data.gameObject.SetActive(isNullOrWhiteSpace);
      }
      Log("Total: ", sw.Elapsed.TotalMilliseconds, " ms");
    }

    private void ClearSearchResult()
    {
      foreach (var data in searchResults)
      {
        searchObjectPool.PutObject(data.gameObject);
      }
      searchResults.Clear();
    }

    private void CreateSearchCraftOption(CraftData data)
    {
      if (data == null)
      {
        Log("Data is null. This shouldn't happen");
        return;
      }
      if (data.fileInfo.Name == "Auto-Saved Ship.craft")
      {
        data.thumbnailLoaded = LoadThumb(GetThumbnailName("Auto-Saved Ship", data.path), out data.thumbnail);
        data.name = data.name + " (Auto-Saved Ship)";
      }
      else
      {
        data.thumbnailLoaded = LoadThumb(GetThumbnailName(data.name, data.path), out data.thumbnail);
      }
      data.gameObject = searchObjectPool.GetObject();//Instantiate(searchTemplate.gameObject);
      //data.gameObject = newCraftOption;
      data.gameObject.name = data.name;
      data.gameObject.SetActive(true);
      data.gameObject.transform.SetParent(viewportContent, false);
      //Log(data.gameObject.activeInHierarchy, data.gameObject.activeSelf, data.gameObject.transform.parent.name);
      InitTextField(data.gameObject.transform, "Name", data.name);
      InitTextField(data.gameObject.transform, "PathLabel", data.directoryInfo.FullName.Replace(mainSavePath, ""));
      //InitImage(data.gameObject.transform, "CraftThumbnail", data.thumbnail);
      var thumbnailImage = InitImage(data.gameObject.transform, "CraftThumbnail", data.thumbnail);
      var onHoverObj = thumbnailImage.GetComponent<OnHover>();
      //FadeCanvasGroup(settingsWindow.canvasGroup, 1, settings.uiFadeSpeed);
      onHoverObj.onHover.AddListener(() =>
        {
          ShowBigThumbnail(data);
        });
      onHoverObj.onExit.AddListener(HideBigThumbnail);
      var button = data.gameObject.GetComponent<DoubleClick>();
      button.onDoubleClick.RemoveAllListeners();
      button.onDoubleClick.AddListener(() =>
      {
        //ClearSearchResult();
        searchInput.text = string.Empty;
        var directoryData = new CraftDirectoryData();
        directoryData.directoryInfo = data.directoryInfo;
        OpenDirectory(directoryData);
        SetActiveCraft(data);
      });
    }

    private void OnAscendingChange(bool arg0)
    {
      Log("OnSortOptionChange");
      settings.ascending = arg0;
      SortCraftsAndFolders();
      SaveSettings();
    }

    private void OnSortOptionChange(int arg0)
    {
      Log("OnSortOptionChange");
      settings.sortOption = (SortOptions)arg0;
      SaveSettings();
    }

    private void OnBackupInterval(BackupInterval arg0)
    {
      Log("OnBackupInterval");
      settings.backupInterval = arg0;
      SaveSettings();
    }

    private void CreateNewDirectory()
    {
      var newDirectoryPath = Path.Combine(currentDirectory.FullName, "New Folder");
      var i = 1;
      while (Directory.Exists(newDirectoryPath))
      {
        newDirectoryPath = Path.Combine(currentDirectory.FullName, "New Folder (" + i + ")");
        i++;
      }
      var newDirectory = Directory.CreateDirectory(newDirectoryPath);
      GenerateDirectoryObject(newDirectory);
      SortCraftsAndFolders();
    }

    private void Rename()
    {
      if (activePath.IsNullOrWhiteSpace())
      {
        Log("No craft selected!");
        return;
      }
      //var craftData;
      if (!activePathIsDirectory)
      {
        //CraftData renameCraftData = null;
        var renameCraftData = GetActiveCraftData();
        if (renameCraftData != null)
        {
          SetInputInteractable(renameCraftData.nameInputField, true);
        }
      }
      else
      {
        CraftDirectoryData renameDirectoryData = GetActiveDirectoryData();
        if (renameDirectoryData == null)
        {
          Log("renameDirectoryData is null ", activePath);
          return;
        }
        SetInputInteractable(renameDirectoryData.input, true);
      }
    }

    private CraftDirectoryData GetActiveDirectoryData()
    {
      /*foreach (var data in directoryInfos)
      {
        if (data.directoryInfo.FullName == activePath)
        {
          return data;
        }
      }*/
      return GetDirectoryData(activePath);
    }

    private CraftDirectoryData GetDirectoryData(string path)
    {
      foreach (var data in directoryInfos)
      {
        if (data == null)
        {
          Log("data is null wtf ?");
          continue;
        }
        if (data.directoryInfo == null)
        {
          Log("data.directoryInfo is null wtf ?");
          continue;
        }
        if (data.directoryInfo.FullName == path)
        {
          return data;
        }
      }
      return null;
    }

    private CraftData GetActiveCraftData()
    {
      foreach (var data in craftData)
      {
        if (data.fileInfo.FullName == activePath)
        {
          return data;
        }
      }
      return null;
    }

    private static void SetInputInteractable(InputField data, bool setTo)
    {
      data.interactable = setTo;
      data.textComponent.raycastTarget = setTo;
      if (setTo)
        data.ActivateInputField();
      else
        data.DeactivateInputField();
    }

    private void ShowHistory()
    {
      if (activePath.IsNullOrWhiteSpace())
      {
        Log("No craft selected!");
        return;
      }
      var historyPath = GetBackupDirectory(activePath);
      if (!Directory.Exists(historyPath))
      {
        Log("Craft has no history!");
        return;
      }
      currentDirectory = new DirectoryInfo(historyPath);
      SetIsHistoryDirectory(true);
      GetCraftFiles();
    }

    private void LoadCraft()
    {
      if (activePath.IsNullOrWhiteSpace())
      {
        Log("No craft selected!");
        return;
      }
      EditorLogic.LoadShipFromFile(activePath);
      currentLoadedCraft = activePath;
      currentCraftData = GetActiveCraftData();
    }

    private void MergeCraft()
    {
      if (activePath.IsNullOrWhiteSpace())
      {
        Log("No craft selected!");
        return;
      }
      if (EditorLogic.fetch.ship.Parts.Count == 0)
      {
        Log("No craft to attach to!");
        return;
      }
      ShipConstruct shipConstruct = GetShipConstruct(activePath);
      EditorLogic.fetch.SpawnConstruct(shipConstruct);
    }

    private static ShipConstruct GetShipConstruct(string path)
    {
      ShipConstruct shipConstruct = new ShipConstruct();
      ConfigNode root = ConfigNode.Load(path);
      shipConstruct.LoadShip(root);
      return shipConstruct;
    }

    #endregion ui

    #region GettingAndProccessingCraftFiles

    private void GetCraftFiles()
    {
      ClearSearchResult();
      uiUpdateDone = false;
      activePath = string.Empty;
      var filesArray = currentDirectory.GetFiles("*.craft", SearchOption.TopDirectoryOnly);//GetFiles(GetFilesPath());
      var newData = new CraftData[filesArray.Length];
      var sw = new Stopwatch();
      var currentTimeStamp = DateTime.Now.ToString("dd.MM.yy_HH:mm:ss:fff");
      lastTimeStamp = currentTimeStamp;
      var l = filesArray.Length;
      sw.Start();
      for (var i = 0; i < filesArray.Length; i++)
      {
        var localI = i;//yes we have to copy it here or it will always only run on the last item
        WorkController.AddWork(() =>
        {//Yes yes using threads adds an overhead but see it on the bright side if you have more crafts especially bigger ones this runs through them faster!
          if (lastTimeStamp == currentTimeStamp)
          {
            newData[localI] = Utilities.Craft.GetCraftInfo(filesArray[localI]);
            //newData[localI].thumbnail = LoadThumb(GetThumbnailPath(newData[localI].name, settings.isVAB));// GetThumbnail(GetThumbnailPath(newData[localI]));
          }
          Interlocked.Decrement(ref l);
          if (l == 0)
          {
            var loadTime = sw.Elapsed.TotalMilliseconds;
            Interlocked.Exchange(ref lastLoadTime, loadTime);
          }
        });
      }
      craftData = newData;
      StartCoroutine(UpdateUI(filesArray));
    }

    private IEnumerator UpdateUI(FileInfo[] files)
    {
      var currentData = craftData;
      var currentTimeStamp = lastTimeStamp;
      yield return null;
      while (viewportContent == null)
      {
        Log("viewportContent == null");
        yield return null;
      }

      if (lastTimeStamp != currentTimeStamp)
      {//abort this coroutine incase a new array is generated during the yield
        Log("craftData != currentData2");
        yield break;
      }
      DeleteChildren(viewportContent);

      var yielded = true;
      for (var i = 0; i < currentData.Length; i++)
      {
        if (!yielded)//to smooth the loading up a bit. Instantiate is a quite heavy operation!
          yield return null;
        yielded = false;
        do
        {
          if (lastTimeStamp != currentTimeStamp)
          {//abort this coroutine incase a new array is generated during the yield
            Log("craftData != currentData2");
            yield break;
          }
          if (currentData[i] != null)
          {
            if (!currentData[i].isDone)
            {
              Log("!data.isDone", i);
              yielded = true;
              yield return null;
            }
          }
          else
          {
            Log("data is null... waiting...");
            yielded = true;
            yield return null;
          }
        } while (currentData[i] == null || !currentData[i].isDone);
        if (!yielded)
        {
          yielded = true;
          yield return null;
        }
        if (lastTimeStamp != currentTimeStamp)
        {//abort this coroutine incase a new array is generated during the yield
          Log("craftData != currentData2");
          yield break;
        }
        var data = currentData[i];
        CreateCraftOption(data);
      }
      uiUpdateDone = true;
      Log("Done ", lastLoadTime);
      SortCraftsAndFolders();
    }

    private void SortCraftsAndFolders()
    {
      var sort = settings.ascending;
      if (isHistoryDirectory)
        sort = false;
      viewportContent.SortChildrenByName(sort);
      //foldersViewportContent.SortChildrenByName(settings.ascending);
    }

    private void OnDropCraft(CraftDirectoryData targetData, Transform sourceTransform)
    {
      var target = targetData.directoryInfo;
      CraftData source = null;
      foreach (var craftInfo in craftData)
      {
        if (craftInfo.gameObject == null)
          continue;
        if (craftInfo.gameObject.transform == sourceTransform)
        {
          source = craftInfo;
          break;
        }
      }

      var targetPath = Path.Combine(target.FullName, source.fileInfo.Name);

      Log(source.fileInfo.FullName, " to ", targetPath);
      var filePath = source.fileInfo.Name;
      var backupDirectoryName = GetBackupDirectoryName(filePath);
      var backupDirectory = Path.Combine(source.fileInfo.DirectoryName, backupDirectoryName);
      var backupDirectoryTarget = Path.Combine(target.FullName, backupDirectoryName);

      Log(backupDirectory, " to ", backupDirectoryTarget);

      var oldThumbnail = GetThumbnailPath(GetThumbnailName(source.name, source.path));
      var newThumbnail = GetThumbnailPath(GetThumbnailName(source.name, targetPath));

      Log(oldThumbnail, " to ", newThumbnail);

      if (File.Exists(targetPath))
      {
        Log("File already exists at: ", targetPath);
        return;
      }
      if (Directory.Exists(backupDirectoryTarget))
      {
        Log("Backup directory already exists at: ", backupDirectoryTarget);
        return;
      }
      Destroy(source.gameObject);
      File.Move(source.fileInfo.FullName, targetPath);
      if (Directory.Exists(backupDirectory))
      {
        Log(backupDirectory, " to ", backupDirectoryTarget);
        Directory.Move(backupDirectory, backupDirectoryTarget);
      }
      else
      {
        Log("Source backup directory doesn't exist: ", backupDirectory);
      }

      if (File.Exists(newThumbnail))
      {
        Log("Thumbnail already exists at: ", targetPath, ". Deleting...");
        File.Delete(newThumbnail);
      }
      if (File.Exists(oldThumbnail))
      {
        Log(oldThumbnail, " to ", newThumbnail);
        File.Move(oldThumbnail, newThumbnail);
      }
      else
      {
        Log("Source thumbnail doesn't exist: ", oldThumbnail);
      }
    }

    private static string GetBackupDirectoryName(string filePath)
    {
      return Path.GetFileNameWithoutExtension(filePath) + historyDirectory;
    }

    private static string GetBackupDirectory(string filePath)
    {
      return Path.Combine(Path.GetDirectoryName(filePath), GetBackupDirectoryName(filePath));
    }

    private void CreateCraftOption(CraftData data)
    {
      if (settings.hideUnloadable && !data.completeCraft)
      {
        Log(data.path, " is unloadable!");
        return;
      }
      bool isAutoSave = false;
      if (data.fileInfo.Name == "Auto-Saved Ship.craft")
      {
        data.thumbnailLoaded = LoadThumb(GetThumbnailName("Auto-Saved Ship", data.path), out data.thumbnail);
        data.name = data.name + " (Auto-Saved Ship)";
        isAutoSave = true;
      }
      else
      {
        data.thumbnailLoaded = LoadThumb(GetThumbnailName(data.name, data.path), out data.thumbnail);
      }
      if (isHistoryDirectory)
      {
        data.name = data.fileInfo.LastWriteTime.ToString("F");
      }
      var newCraftOption = Instantiate(template.gameObject);
      data.gameObject = newCraftOption;
      UpdateCraftGameObjectName(data);
      newCraftOption.transform.SetParent(viewportContent, false);
      newCraftOption.SetActive(true);

      var partAndStageCount = new StringBuilder();
      partAndStageCount.Append(data.partCount);
      if (data.partCount == 1)
        partAndStageCount.Append(" part in ");
      else
        partAndStageCount.Append(" parts in ");
      partAndStageCount.Append(data.stageCount);
      if (data.partCount == 1)
        partAndStageCount.Append(" stage.");
      else
        partAndStageCount.Append(" stages.");

      data.nameInputField = InitInputField(newCraftOption.transform, "Name", data.name);
      if (!data.completeCraft)
      {
        Log("Line 1137 Display incomplete craft warning!");
      }

      SetInputInteractable(data.nameInputField, false);

      InitTextField(newCraftOption.transform, "PartsAndStageCount", partAndStageCount.ToString());
      InitTextField(newCraftOption.transform, "Cost", data.cost.ToString());

      var thumbnailImage = InitImage(newCraftOption.transform, "CraftThumbnail", data.thumbnail);
      var onHoverObj = thumbnailImage.GetComponent<OnHover>();
      onHoverObj.onHover.AddListener(
        () =>
        {
          ShowBigThumbnail(data);
        }
        );
      onHoverObj.onExit.AddListener(HideBigThumbnail);
      if (currentCraftData != null)
      {
        if (data.path == currentCraftData.path)
        {
          currentCraftData = data;
        }
      }
      var craftToggle = newCraftOption.GetComponent<Toggle>();
      if (!activePathIsDirectory)
      {
        if (activePath == data.fileInfo.FullName)
          craftToggle.isOn = true;
      }
      craftToggle.onValueChanged.AddListener((isOn) =>
      {
        if (isOn)
        {
          SetActiveCraft(data);
        }
      });
      data.nameInputField.onEndEdit.AddListener((newName) =>
      {
        SetInputInteractable(data.nameInputField, false);
        if (isAutoSave)
        {
          data.nameInputField.text = data.name;
          return;
        }
        RenameCraft(data, newName);
      });
      data.contextMenu = data.gameObject.GetComponent<UI.ContextMenu>();
      data.contextMenu.Init();
      data.contextMenuCopy = data.contextMenu.AddOption("Copy");
      data.contextMenuCopy.button.onClick.AddListener(() =>
      {
        Log("Copy");
        copyCraftData = data;
      });
    }

    private void HideBigThumbnail()
    {
      bigThumbnailImage.gameObject.SetActive(false);
    }

    private void ShowBigThumbnail(CraftData data)
    {
      if (!data.thumbnailLoaded)
        return;
      bigThumbnailImage.gameObject.SetActive(true);
      bigThumbnailImage.sprite = GetSprite(data.thumbnail);
    }

    private void SetActiveCraft(CraftData data)
    {
      activePathIsDirectory = false;
      activePath = data.fileInfo.FullName;
    }

    private void UpdateCraftGameObjectName(CraftData data)
    {
      switch (settings.sortOption)
      {
        case SortOptions.Name:
          data.gameObject.name = "2 - Craft - " + data.name;
          break;

        case SortOptions.Parts:
          data.gameObject.name = "2 - Craft - " + data.partCount;
          break;

        case SortOptions.Stages:
          data.gameObject.name = "2 - Craft - " + data.stageCount;
          break;

        case SortOptions.Cost:
          data.gameObject.name = "2 - Craft - " + data.cost;
          break;
      }
    }

    private void RenameCraft(CraftData data, string newName)
    {
      if (data.name == newName)
      {
        Log("Old name and new name matches");
        return;
      }
      Log("Old name: ", data.name, " setting to ", newName);
      data.name = newName;
      data.configNode.SetValue("ship", newName);
      Log("Old file at ", data.path);
      if (File.Exists(data.path))
      {
        Log("Old file exists! Deleting....");
        File.Delete(data.path);
      }
      var oldBackupPath = GetBackupDirectory(data.path);
      data.path = Path.Combine(data.directoryInfo.FullName, KSPUtil.SanitizeString(newName, ' ', false) + ".craft");
      Log("New file at ", data.path, " Saving....");
      data.configNode.Save(data.path);
      var newBackupPath = GetBackupDirectory(data.path);
      Log("Old backup directory: ", oldBackupPath, " New backup directory: ", newBackupPath);
      if (Directory.Exists(oldBackupPath))
      {
        Log("Old backup directory exists. Checking new target.");
        if (Directory.Exists(newBackupPath))
        {
          Log("New backup directory exists. Deleting...");
          Directory.Delete(newBackupPath);
        }
        Log("Moving backup directory....");
        Directory.Move(oldBackupPath, newBackupPath);
      }
      UpdateCraftGameObjectName(data);
      SortCraftsAndFolders();
    }

    private string GetThumbnailName(string name, string path)
    {
      path = Path.GetDirectoryName(path).Replace(mainSavePath, "");
      var thumbnailName = new StringBuilder();
      thumbnailName.Append(HighLogic.SaveFolder);
      thumbnailName.Append("_");
      thumbnailName.Append(path);
      thumbnailName.Append("_");
      thumbnailName.Append(KSPUtil.SanitizeString(name, '_', false));
      return Utilities.GetMD5Hash(thumbnailName.ToString());
    }

    private string GetFilesPath()
    {
      var path = mainSavePath;
      if (settings.isVAB)
      {
        path = Path.Combine(path, "VAB");
      }
      else
      {
        path = Path.Combine(path, "SPH");
      }
      return path;
    }

    private string[] GetFiles(string path)
    {
      if (Directory.Exists(path))
        return Directory.GetFiles(path, "*.craft", SearchOption.TopDirectoryOnly);
      else
        return new string[0];
    }

    private bool LoadThumb(string FileName, out Texture2D thumb)
    {
      //Texture2D thumb;
      /*if (thumbCache.TryGetValue(FileName, out thumb))
      {
        return true;
      }*/
      string thumbPath = GetThumbnailPath(FileName);
      if (File.Exists(thumbPath))
      {
        thumb = new Texture2D(0, 0);
        thumb.LoadImage(File.ReadAllBytes(thumbPath));
      }
      else
      {
        thumb = stockTexture;
        return false;
      }
      //thumbCache.Add(FileName, thumb);
      return true;
    }

    private string GetThumbnailPath(string FileName)
    {
      return applicationRootPath + "/thumbs/" + FileName + ".png";
    }

    #endregion GettingAndProccessingCraftFiles

    #region toolbar

    public List<GameScenes> activeScences
    {
      get
      {
        return _activeScences;
      }
    }

    public UnityAction onClick
    {
      get
      {
        return OnToolbar;
      }
    }

    private void OnToolbar()
    {
      settings.showSettings = !settings.showSettings;
      if (settings.showSettings)
      {
        FadeCanvasGroup(settingsWindow.canvasGroup, 1, settings.uiFadeSpeed);
      }
      else
      {
        FadeCanvasGroup(settingsWindow.canvasGroup, 0, settings.uiFadeSpeed);
      }
    }

    public Sprite icon
    {
      get
      {
        return AssetLoader.GetAsset<Sprite>("CraftHistory", "Icons", "CraftHistory/CraftHistory");//Utilities.GetTexture("icon", "CraftHistory/Textures");
      }
    }

    #endregion toolbar

    #region API

    public int GetRevisions(string path)
    {
      var backupPath = GetBackupDirectory(path);
      if (!Directory.Exists(backupPath))
        return 0;
      var directory = new DirectoryInfo(backupPath);
      return directory.GetFiles("*.craft", SearchOption.TopDirectoryOnly).Length;
    }

    public string GetThumbnailName(string path)
    {
      var fullPath = Path.GetFullPath(path);
      var name = Path.GetFileNameWithoutExtension(fullPath);
      return GetThumbnailName(fullPath, name);
    }

    #endregion API
  }
}