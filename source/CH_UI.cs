using KerboKatz.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace KerboKatz
{
  partial class CraftHistory : KerboKatzBase
  {
    private bool categoriesModified;
    private bool catsSetAlready;
    private bool exInfoSetAlready;
    private bool hideUnloadableCrafts;
    private bool historyOnDemand;
    private bool initStyle = false;
    private bool saveAll;
    private bool saveInInterval;
    private bool showLoadWindow = false;
    private bool toggleAllCats;
    private bool toggleAllExtendedInfo;
    private Dictionary<string, bool> toggleCategories = new Dictionary<string, bool>();
    private Dictionary<string, bool> toggleExtendedInfo = new Dictionary<string, bool>();
    private GUIStyle addCatLoadIconStyle;
    private GUIStyle areaStyle;
    private GUIStyle arrowBrightStyle;
    private GUIStyle arrowStyle;
    private GUIStyle ascDescButtonStyle;
    private GUIStyle buttonDeleteIconStyle;
    private GUIStyle buttonEditCatIconStyle;
    private GUIStyle buttonHistoryIconStyle;
    private GUIStyle buttonLoadIconStyle;
    private GUIStyle buttonStyle;
    private GUIStyle buttonStyle200;
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
    private GUIStyle prevButtonStyle;
    private GUIStyle scrollview;
    private GUIStyle searchFieldStyle;
    private GUIStyle searchTextStyle;
    private GUIStyle settingsWindowStyle;
    private GUIStyle showHideAllTextStyle;
    private GUIStyle sortOptionTextStyle;
    private GUIStyle sortTextStyle;
    private GUIStyle textStyle;
    private GUIStyle textStyleRed;
    private GUIStyle toggleStyle;
    private GUIStyle verticalScrollbar;
    private static int editCategoriesWindowID = Utilities.UI.getNewWindowID;
    private static int editExistingCraftCategoriesWindowID = Utilities.UI.getNewWindowID;
    private static int historyWindowID = Utilities.UI.getNewWindowID;
    private static int loadCraftID = Utilities.UI.getNewWindowID;
    private static int settingsWindowID = Utilities.UI.getNewWindowID;
    private int sortOption = 0;
    private int sortOrder = 0;
    private string addToCategoryString = "";
    private string delimiter;
    private string historyFiles;
    private string saveInterval;
    private string searchCraft = "";
    private string showHistory;
    private Vector2 catWindowScrollPosition = new Vector2();
    private Vector2 loadWindowScrollPosition = new Vector2();
    private Vector2 scrollPositionHistory;
    private Rectangle editCategoriesWindow = new Rectangle(Rectangle.updateType.Cursor);
    private Rectangle editExistingCraftCategoriesWindow = new Rectangle(Rectangle.updateType.Cursor);
    private Rectangle historyWindow = new Rectangle(Rectangle.updateType.Cursor);
    private Rectangle loadWindowPosition = new Rectangle(Rectangle.updateType.Center);
    private Rectangle settingsWindow = new Rectangle(Rectangle.updateType.Cursor);
    private string currentEditor;
    private static Dictionary<string, Texture2D> cachedThumbs = new Dictionary<string, Texture2D>();
    private GUIStyle craftStyleShort;
    private GUIStyle thumbnailStyle;
    private GUIStyle toolbarOptionLabelStyle;
    private bool deleteThumbnails;
    private GUIStyle areaStyleExpanded;
    private GUIStyle emptyStyle;

    private GUIStyle verticalScrollbarThumb;
    private GUIStyle arrowBrightCatStyle;

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
        Utilities.UI.createWindow(currentSettings.getBool("showSettings"), settingsWindowID, ref settingsWindow, createSettingsWindow, "CraftHistory Settings", settingsWindowStyle, true);
        Utilities.UI.createWindow(currentSettings.getBool("showEditCategories"), editCategoriesWindowID, ref editCategoriesWindow, editCategories, "Edit craft categories", editCategoriesWindowStyle, true);
        Utilities.UI.createWindow(showLoadWindow, loadCraftID, ref loadWindowPosition, loadWindow, "Select a craft to load", loadWindowStyle, true);
        Utilities.UI.createWindow(!String.IsNullOrEmpty(showHistory), historyWindowID, ref historyWindow, createHistoryWindow, "Select a craft from history to load", loadWindowStyle, true);
        Utilities.UI.createWindow(!String.IsNullOrEmpty(existingCraftCategoriesFile), editExistingCraftCategoriesWindowID, ref editExistingCraftCategoriesWindow, editExistingCraftCategories, "Edit craft categories", editCategoriesWindowStyle, true);
        Utilities.UI.showTooltip();
      }
    }

    private void InitStyle()
    {
      emptyStyle = new GUIStyle(HighLogic.Skin.label);
      emptyStyle.padding.setToZero();
      emptyStyle.margin.setToZero();
      emptyStyle.border.setToZero();

      labelStyle = new GUIStyle(HighLogic.Skin.label);
      labelStyle.stretchWidth = true;

      loadWindowStyle = new GUIStyle(HighLogic.Skin.window);
      loadWindowStyle.fixedWidth = 350;
      loadWindowStyle.padding.left = 0;
      loadWindowStyle.fixedHeight = 505;

      settingsWindowStyle = new GUIStyle(HighLogic.Skin.window);
      settingsWindowStyle.fixedWidth = 250;
      settingsWindowStyle.padding.left = 0;

      editCategoriesWindowStyle = new GUIStyle(HighLogic.Skin.window);
      editCategoriesWindowStyle.fixedWidth = 350;
      editCategoriesWindowStyle.fixedHeight = 505;
      editCategoriesWindowStyle.padding.left = 0;

      toggleStyle = new GUIStyle(HighLogic.Skin.toggle);
      toggleStyle.normal.textColor = labelStyle.normal.textColor;
      toggleStyle.active.textColor = labelStyle.normal.textColor;

      arrowStyle = new GUIStyle(toggleStyle);
      arrowStyle.active.background = Utilities.getTexture("CraftHistoryToggle_off_hover", "CraftHistory/Textures");
      arrowStyle.onActive.background = Utilities.getTexture("CraftHistoryToggle_on_hover", "CraftHistory/Textures");
      arrowStyle.normal.background = Utilities.getTexture("CraftHistoryToggle_off", "CraftHistory/Textures");
      arrowStyle.onNormal.background = Utilities.getTexture("CraftHistoryToggle_on", "CraftHistory/Textures");
      arrowStyle.hover.background = Utilities.getTexture("CraftHistoryToggle_off_hover", "CraftHistory/Textures");
      arrowStyle.onHover.background = Utilities.getTexture("CraftHistoryToggle_on_hover", "CraftHistory/Textures");

      arrowStyle.fixedHeight = 20;
      arrowStyle.fixedWidth = 20;
      arrowStyle.padding.setToZero();
      arrowStyle.overflow.setToZero();
      arrowStyle.margin.top = 4;
      arrowStyle.margin.left = 4;

      arrowBrightCatStyle = new GUIStyle(arrowStyle);
      arrowBrightCatStyle.active.background = Utilities.getTexture("CraftHistoryToggle_bottom_off_hover", "CraftHistory/Textures");
      arrowBrightCatStyle.onActive.background = Utilities.getTexture("CraftHistoryToggle_bottom_on_hover", "CraftHistory/Textures");
      arrowBrightCatStyle.normal.background = Utilities.getTexture("CraftHistoryToggle_bottom_off", "CraftHistory/Textures");
      arrowBrightCatStyle.onNormal.background = Utilities.getTexture("CraftHistoryToggle_bottom_on", "CraftHistory/Textures");
      arrowBrightCatStyle.hover.background = Utilities.getTexture("CraftHistoryToggle_bottom_off_hover", "CraftHistory/Textures");
      arrowBrightCatStyle.onHover.background = Utilities.getTexture("CraftHistoryToggle_bottom_on_hover", "CraftHistory/Textures");

      arrowBrightStyle = new GUIStyle(arrowStyle);
      arrowBrightStyle.margin.setToZero();
      arrowBrightStyle.active.background = Utilities.getTexture("CraftHistoryToggle_bottom_off_hover", "CraftHistory/Textures");
      arrowBrightStyle.onActive.background = Utilities.getTexture("CraftHistoryToggle_bottom_on_hover", "CraftHistory/Textures");
      arrowBrightStyle.normal.background = Utilities.getTexture("CraftHistoryToggle_bottom_off", "CraftHistory/Textures");
      arrowBrightStyle.onNormal.background = Utilities.getTexture("CraftHistoryToggle_bottom_on", "CraftHistory/Textures");
      arrowBrightStyle.hover.background = Utilities.getTexture("CraftHistoryToggle_bottom_off_hover", "CraftHistory/Textures");
      arrowBrightStyle.onHover.background = Utilities.getTexture("CraftHistoryToggle_bottom_on_hover", "CraftHistory/Textures");

      textStyle = new GUIStyle(HighLogic.Skin.label);
      textStyle.fixedWidth = 150;
      textStyle.margin.left = 10;

      textStyleRed = new GUIStyle(textStyle);
      textStyleRed.normal.textColor = Color.red;

      searchTextStyle = new GUIStyle(textStyle);
      searchTextStyle.margin.top = 4;
      searchTextStyle.padding.top = 0;
      searchTextStyle.fixedWidth = 50;

      showHideAllTextStyle = new GUIStyle(searchTextStyle);
      showHideAllTextStyle.fixedWidth = 85;
      showHideAllTextStyle.margin.top = 2;

      sortTextStyle = new GUIStyle(textStyle);
      sortTextStyle.margin.top = 2;
      sortTextStyle.padding.top = 0;
      sortTextStyle.fixedWidth = 60;

      sortOptionTextStyle = new GUIStyle(sortTextStyle);
      sortOptionTextStyle.margin.left = 0;
      sortOptionTextStyle.padding.left = 0;
      sortOptionTextStyle.fixedWidth = 80;
      sortOptionTextStyle.alignment = TextAnchor.MiddleCenter;

      craftStyle = new GUIStyle(HighLogic.Skin.label);
      craftStyle.fixedWidth = 260;
      craftStyle.margin.left = 10;

      craftStyleShort = new GUIStyle(craftStyle);
      craftStyleShort.fixedWidth = 180;
      craftStyleShort.margin.left = 10;
      craftStyleShort.fontSize = 9;

      thumbnailStyle = new GUIStyle(HighLogic.Skin.label);
      thumbnailStyle.padding.setToZero();
      thumbnailStyle.margin.setToZero();
      thumbnailStyle.border.setToZero();
      thumbnailStyle.fixedHeight = 50;
      thumbnailStyle.fixedWidth = 50;
      thumbnailStyle.margin.top = 7;
      thumbnailStyle.margin.left = 15;

      craftNameStyle = new GUIStyle(craftStyle);
      craftNameStyle.fixedWidth = 137;//148;
      craftNameStyle.margin.left = 5;
      craftNameStyle.margin.top = 3;
      craftNameStyle.fontStyle = FontStyle.Bold;
      craftNameStyle.wordWrap = false;

      categoryTextStyle = new GUIStyle(craftStyle);
      categoryTextStyle.fontStyle = FontStyle.Bold;
      categoryTextStyle.padding.setToZero();
      categoryTextStyle.margin.setToZero();
      categoryTextStyle.margin.top = 7;
      categoryTextStyle.fixedHeight = 30;

      categoryEditTextStyle = new GUIStyle(categoryTextStyle);
      categoryEditTextStyle.fixedWidth = 276;
      categoryEditTextStyle.padding.top = 5;

      editCatAddStyle = new GUIStyle(HighLogic.Skin.box);
      editCatAddStyle.fixedHeight = 30;

      containerStyle = new GUIStyle(GUI.skin.button);
      containerStyle.fixedWidth = 230;
      containerStyle.margin.left = 10;

      numberFieldStyle = new GUIStyle(HighLogic.Skin.box);
      numberFieldStyle.fixedWidth = 52;
      numberFieldStyle.fixedHeight = 22;
      numberFieldStyle.alignment = TextAnchor.MiddleCenter;
      numberFieldStyle.margin.left = 45;
      numberFieldStyle.padding.right = 7;
      numberFieldStyle.margin.top = 4;

      searchFieldStyle = new GUIStyle(HighLogic.Skin.box);
      searchFieldStyle.fixedHeight = 22;

      buttonDeleteIconStyle = new GUIStyle(GUI.skin.button);
      buttonDeleteIconStyle.fixedWidth = 30;
      buttonDeleteIconStyle.fixedHeight = 30;
      buttonDeleteIconStyle.margin.top = 0;
      buttonDeleteIconStyle.normal.background = Utilities.getTexture("button_Delete", "CraftHistory/Textures");
      buttonDeleteIconStyle.hover.background = Utilities.getTexture("button_Delete_mouseover", "CraftHistory/Textures");
      buttonDeleteIconStyle.active = buttonDeleteIconStyle.hover;

      buttonHistoryIconStyle = new GUIStyle(buttonDeleteIconStyle);
      buttonHistoryIconStyle.normal.background = Utilities.getTexture("button_History", "CraftHistory/Textures");
      buttonHistoryIconStyle.hover.background = Utilities.getTexture("button_History_mouseover", "CraftHistory/Textures");
      buttonHistoryIconStyle.active = buttonHistoryIconStyle.hover;

      buttonEditCatIconStyle = new GUIStyle(buttonDeleteIconStyle);
      buttonEditCatIconStyle.normal.background = Utilities.getTexture("button_Edit", "CraftHistory/Textures");
      buttonEditCatIconStyle.hover.background = Utilities.getTexture("button_Edit_mouseover", "CraftHistory/Textures");
      buttonEditCatIconStyle.active = buttonEditCatIconStyle.hover;

      buttonLoadIconStyle = new GUIStyle(buttonDeleteIconStyle);
      buttonLoadIconStyle.normal.background = Utilities.getTexture("button_Load", "CraftHistory/Textures");
      buttonLoadIconStyle.hover.background = Utilities.getTexture("button_Load_mouseover", "CraftHistory/Textures");
      buttonLoadIconStyle.fixedWidth = 26;
      buttonLoadIconStyle.fixedHeight = 26;
      buttonLoadIconStyle.active = buttonLoadIconStyle.hover;
      buttonLoadIconStyle.border.setToZero();
      buttonLoadIconStyle.margin.setToZero();
      buttonLoadIconStyle.padding.setToZero();
      buttonLoadIconStyle.margin.left = 5;
      buttonLoadIconStyle.margin.top = 1;


      buttonDeleteIconStyle = new GUIStyle(buttonLoadIconStyle);
      buttonDeleteIconStyle.normal.background = Utilities.getTexture("button_Delete", "CraftHistory/Textures");
      buttonDeleteIconStyle.hover.background = Utilities.getTexture("button_Delete_mouseover", "CraftHistory/Textures");
      buttonDeleteIconStyle.active = buttonDeleteIconStyle.hover;

      buttonHistoryIconStyle = new GUIStyle(buttonLoadIconStyle);
      buttonHistoryIconStyle.normal.background = Utilities.getTexture("button_History", "CraftHistory/Textures");
      buttonHistoryIconStyle.hover.background = Utilities.getTexture("button_History_mouseover", "CraftHistory/Textures");
      buttonHistoryIconStyle.active = buttonHistoryIconStyle.hover;

      buttonEditCatIconStyle = new GUIStyle(buttonLoadIconStyle);
      buttonEditCatIconStyle.normal.background = Utilities.getTexture("button_Edit", "CraftHistory/Textures");
      buttonEditCatIconStyle.hover.background = Utilities.getTexture("button_Edit_mouseover", "CraftHistory/Textures");
      buttonEditCatIconStyle.active = buttonEditCatIconStyle.hover;

      addCatLoadIconStyle = new GUIStyle(buttonDeleteIconStyle);
      addCatLoadIconStyle.normal.background = Utilities.getTexture("button_Save", "CraftHistory/Textures");
      addCatLoadIconStyle.hover.background = Utilities.getTexture("button_Save_mouseover", "CraftHistory/Textures");
      addCatLoadIconStyle.active = buttonLoadIconStyle.hover;


      prevButtonStyle = new GUIStyle(GUI.skin.button);
      prevButtonStyle.fixedWidth = 20;
      prevButtonStyle.fixedHeight = 20;
      prevButtonStyle.normal.background = GUI.skin.button.normal.background;
      prevButtonStyle.hover.background = GUI.skin.button.hover.background;
      prevButtonStyle.active = buttonLoadIconStyle.hover;

      ascDescButtonStyle = new GUIStyle(prevButtonStyle);
      ascDescButtonStyle.fixedWidth = 40;
      ascDescButtonStyle.fixedHeight = 20;

      buttonStyle = new GUIStyle(GUI.skin.button);
      buttonStyle.fixedWidth = 100;
      buttonStyle.alignment = TextAnchor.MiddleCenter;

      buttonStyle200 = new GUIStyle(buttonStyle);
      buttonStyle200.fixedWidth = 200;

      scrollview = new GUIStyle(HighLogic.Skin.scrollView);
      scrollview.padding.right = 0;
      scrollview.padding.left = 2;
      scrollview.margin.right = 0;
      scrollview.margin.left = 5;

      categoryStyle = new GUIStyle(HighLogic.Skin.box);
      categoryStyle.margin.top = 5;
      categoryStyle.fixedWidth = 330;
      categoryStyle.onHover = categoryStyle.normal;
      categoryStyle.hover = categoryStyle.normal;


      areaStyle = new GUIStyle(HighLogic.Skin.button);
      areaStyle.padding.setToZero();
      areaStyle.margin.setToZero();
      areaStyle.fixedWidth = 322;
      areaStyleExpanded = new GUIStyle(areaStyle);
      areaStyleExpanded.fixedHeight = 90;
      areaStyleExpanded.fixedWidth = 322;

      areaStyle.onHover = areaStyle.normal;
      areaStyle.hover = areaStyle.normal;
      areaStyle.margin.left = 0;
      areaStyle.margin.right = 0;

      verticalScrollbar = new GUIStyle(HighLogic.Skin.verticalScrollbar);
      verticalScrollbar.padding.left = 0;
      verticalScrollbar.padding.right = 0;
      verticalScrollbar.margin.left = 0;
      verticalScrollbar.margin.right = 0;
      verticalScrollbar.fixedWidth = 8;
      verticalScrollbar.hover.background = Utilities.getTexture("verticalScrollbar", "CraftHistory/Textures");
      verticalScrollbar.active.background = Utilities.getTexture("verticalScrollbar", "CraftHistory/Textures");
      verticalScrollbar.normal.background = Utilities.getTexture("verticalScrollbar", "CraftHistory/Textures");
      verticalScrollbar.border.top = 6;
      verticalScrollbar.border.bottom = 6;

      verticalScrollbarThumb = new GUIStyle();
      verticalScrollbarThumb.fixedWidth = 8;
      verticalScrollbarThumb.border.top = 6;
      verticalScrollbarThumb.border.bottom = 6;
      verticalScrollbarThumb.border.left = 4;
      verticalScrollbarThumb.border.right = 4;
      verticalScrollbarThumb.hover.background = Utilities.getTexture("verticalScrollbarThumb", "CraftHistory/Textures");
      verticalScrollbarThumb.active.background = Utilities.getTexture("verticalScrollbarThumb", "CraftHistory/Textures");
      verticalScrollbarThumb.normal.background = Utilities.getTexture("verticalScrollbarThumb", "CraftHistory/Textures"); 

      if (Utilities.UI.sortTextStyle == null)
        Utilities.UI.getTooltipStyle();
      toolbarOptionLabelStyle = new GUIStyle(Utilities.UI.sortTextStyle);
      toolbarOptionLabelStyle.padding.left += 6;

      initStyle = true;
    }

    private void editCategoriesBegin()
    {
      GUILayout.BeginVertical();
      catWindowScrollPosition = Utilities.UI.beginScrollView(catWindowScrollPosition, 340, 390, false, true, GUIStyle.none, null, scrollview);
    }

    private Texture2D GetThumbnail(String FileName, bool ignoreCache = false)
    {
      if (!cachedThumbs.ContainsKey(FileName))
      {
        cachedThumbs.Add(FileName, LoadThumb(FileName));
      }
      else if (cachedThumbs[FileName].width == 0 || ignoreCache)
      {
        cachedThumbs[FileName] = LoadThumb(FileName);
      }
      return cachedThumbs[FileName];
    }

    private Texture2D LoadThumb(String FileName)
    {
      var thumb = new Texture2D(0, 0);
      var thumbPath = KSPUtil.ApplicationRootPath + "/thumbs/" + FileName + ".png";
      if (System.IO.File.Exists(thumbPath))
      {
        thumb.LoadImage(System.IO.File.ReadAllBytes(thumbPath));
      }
      return thumb;
    }

    private void editCategories(int id)
    {
      editCategoriesBegin();
      List<string> toBeRemoved = new List<string>();
      foreach (string cat in currentCraftCategories)
      {
        if (cat.IsNullOrWhiteSpace())
          continue;
        displayCraftCategory(ref toBeRemoved, cat);
      }
      GUILayout.EndScrollView();
      createAddToCategoryButton(ref currentCraftCategories);
      GUILayout.EndVertical();
      GUI.DragWindow();
      foreach (var rem in toBeRemoved)
      {
        currentCraftCategories.Remove(rem);
      }
      Utilities.UI.updateTooltipAndDrag();
    }

    private void editExistingCraftCategories(int id)
    {
      editCategoriesBegin();
      List<string> toBeRemoved = new List<string>();
      foreach (string cat in existingCraftCategories)
      {
        if (cat.IsNullOrWhiteSpace())
          continue;
        displayCraftCategory(ref toBeRemoved, cat);
      }
      GUILayout.EndScrollView();
      createAddToCategoryButton(ref existingCraftCategories);
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      if (Utilities.UI.createButton("Save categories to craft file", buttonStyle200, "This might take a few seconds."))
      {
        if (filesDic.ContainsKey(existingCraftCategoriesFile))
        {
          ThreadPool.QueueUserWorkItem(new WaitCallback(saveCraft), new object[] { existingCraftCategoriesFile, existingCraftCategories, currentSettings.getString("historyOnDemand"), getSavePath(), filesDic[existingCraftCategoriesFile].craftName });
          if (EditorLogic.fetch.ship.shipName == filesDic[existingCraftCategoriesFile].craftName)
          {
            currentCraftCategories.Clear();
            foreach (var cat in existingCraftCategories)
            {
              currentCraftCategories.AddUnique(cat);
            }
          }
        }
      }
      if (Utilities.UI.createButton("Close", buttonStyle))
      {
        existingCraftCategoriesFile = null;
      }
      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
      GUI.DragWindow();
      foreach (var rem in toBeRemoved)
      {
        existingCraftCategories.Remove(rem);
      }
      Utilities.UI.updateTooltipAndDrag();
    }

    private void displayCraftCategory(ref List<string> toBeRemoved, string cat)
    {
      GUILayout.BeginVertical(areaStyle);
      GUILayout.BeginHorizontal();
      Utilities.UI.createLabel(cat, categoryEditTextStyle);
      if (Utilities.UI.createButton("", buttonDeleteIconStyle, "Once you finished editing categories you have to save your craft to apply the changes!"))
      {
        toBeRemoved.Add(cat);
      }
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
    }

    private void createAddToCategoryButton(ref List<string> categories)
    {
      GUILayout.FlexibleSpace();
      GUILayout.BeginHorizontal();
      Utilities.UI.createLabel("Add to category:", textStyle);
      addToCategoryString = GUILayout.TextField(addToCategoryString, int.MaxValue, editCatAddStyle);
      if (Utilities.UI.createButton("", addCatLoadIconStyle, "Once you finished editing categories you have to save your craft to apply the changes!"))
      {
        categories.AddUnique(addToCategoryString);
      }
      GUILayout.EndHorizontal();
      GUILayout.FlexibleSpace();
    }

    private void createSettingsWindow(int id)
    {
      GUILayout.BeginVertical();
      if (GUILayout.Toggle(historyOnDemand, new GUIContent("History on demand", "Enable to create a history point during saving of the craft"), toggleStyle))
      {
        historyOnDemand = true;
        saveAll = false;
        saveInInterval = false;
      }
      else
      {
        historyOnDemand = false;
      }
      if (GUILayout.Toggle(saveAll, new GUIContent("Save every iteration of the craft", "Enable to save every change of the craft"), toggleStyle))
      {
        saveAll = true;
        historyOnDemand = false;
        saveInInterval = false;
      }
      else
      {
        saveAll = false;
      }
      if (GUILayout.Toggle(saveInInterval, new GUIContent("Save in interval", "Enable to only save the latest iteration of the craft after X seconds"), toggleStyle))
      {
        saveInInterval = true;
        historyOnDemand = false;
        saveAll = false;
      }
      else
      {
        saveInInterval = false;
      }
      GUILayout.BeginHorizontal();
      GUI.enabled = saveInInterval;
      Utilities.UI.createLabel("Save Interval (sec):", textStyle, "Saves the craft after these seconds");
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
      if (GUILayout.Toggle(deleteThumbnails, new GUIContent("Delete thumbnails", "When this option is enabled CraftHistory will delete the thumbnails associated with the craft"), toggleStyle))
      {
        deleteThumbnails = true;
      }
      else
      {
        deleteThumbnails = false;
      }

      GUILayout.BeginHorizontal();
      Utilities.UI.createLabel("Delimiter:", textStyle, "Save crafts with a prefix and this delimter to create categories.");
      delimiter = GUILayout.TextField(delimiter, 1, numberFieldStyle);
      GUILayout.EndHorizontal();
      GUILayout.Space(10);
      GUILayout.BeginHorizontal();
      Utilities.UI.createOptionSwitcher("Sort by:", sortOptions[sortOrder], ref sortOption, sortTextStyle, sortOptionTextStyle, prevButtonStyle, prevButtonStyle);

      if (GUILayout.Button("▲▼", ascDescButtonStyle))
      {
        if (sortOrder == 0)
        {
          sortOrder = 1;
        }
        else
        {
          sortOrder = 0;
        }
      }
      GUILayout.EndHorizontal();

      Utilities.UI.createOptionSwitcher("Use:", Toolbar.toolbarOptions, ref toolbarSelected, toolbarOptionLabelStyle);

      GUILayout.Space(10);
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("Save", buttonStyle))
      {
        updateToolbarBool();
        currentSettings.set("saveAll", saveAll);
        currentSettings.set("saveInInterval", saveInInterval);
        currentSettings.set("hideUnloadableCrafts", hideUnloadableCrafts);
        currentSettings.set("saveInterval", saveInterval);
        currentSettings.set("delimiter", delimiter);
        currentSettings.set("historyOnDemand", historyOnDemand);
        currentSettings.set("deleteThumbnails", deleteThumbnails);
        if (currentSettings.getInt("sortOption") != sortOption || currentSettings.getInt("sortOrder") != sortOrder)
        {
          currentSettings.set("sortOption", sortOption);
          currentSettings.set("sortOrder", sortOrder);
          categoriesModified = true;
        }
        currentSettings.save();
        currentSettings.set("showSettings", false);
      }
      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
      Utilities.UI.updateTooltipAndDrag();
    }

    private void loadWindow(int windowID)
    {

      GUILayout.BeginVertical();
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      if (Utilities.UI.createButton("VAB", buttonStyle, (currentSettings.getString("editorScene") == "VAB")))
      {
        changePathTo("VAB");
      }
      if (Utilities.UI.createButton("SPH", buttonStyle, (currentSettings.getString("editorScene") == "SPH")))
      {
        changePathTo("SPH");
      }
      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
      Utilities.UI.createLabel("Search:", searchTextStyle);
      searchCraft = GUILayout.TextField(searchCraft, int.MaxValue, searchFieldStyle);
      GUILayout.EndHorizontal();
      var before = loadWindowScrollPosition;
      loadWindowScrollPosition = Utilities.UI.beginScrollView(loadWindowScrollPosition, 340, 400, false, true, GUIStyle.none,null,scrollview);
      var scrollOffset = loadWindowScrollPosition - before;
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
        var craftInfo = filesDic[file];
        var category = pair.Key;
        if (!craftInfo.craftComplete && currentSettings.getBool("hideUnloadableCrafts"))
          continue;
        if (!createCategory(ref currentCategory, ref startedCategory, category))
          continue;

        displayCraft(file, craftInfo);
        scrollOffset = updateDragAndDrop(scrollOffset, file);

        GUILayout.Space(2);

      }
      if (startedCategory)
      {
        GUILayout.EndVertical();
      }
      GUILayout.EndScrollView();
      GUILayout.BeginHorizontal();

      createToggleAllButtons();
      GUILayout.FlexibleSpace();
      if (Utilities.UI.createButton("Close", buttonStyle))
      {
        showLoadWindow = false;
        showHistory = null;
      }
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
      Utilities.UI.updateTooltipAndDrag(200, new Rect(0, 0, 200, 25));
    }

    private bool createCategory(ref string currentCategory, ref bool startedCategory, string category)
    {
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
          if (GUILayout.Toggle(toggleCategories[category], new GUIContent("", "Show/hide " + currentCategory), arrowBrightCatStyle))
          {
            toggleCategories[category] = true;
          }
          else
          {
            toggleCategories[category] = false;
          }
          Utilities.UI.createLabel(currentCategory, categoryTextStyle);
          GUILayout.EndHorizontal();
        }
        if (toggleCategories.ContainsKey(category) && !toggleCategories[category])
          return false;
      }
      else
      {
        if (startedCategory)
        {
          if (toggleCategories.ContainsKey(currentCategory) && toggleCategories[currentCategory])
            GUILayout.Space(5);
          GUILayout.EndVertical();
          startedCategory = false;
        }
      }
      return true;
    }

    private Vector2 updateDragAndDrop(Vector2 scrollOffset, string file)
    {
      if (filesDic[file].drag && (Event.current.type == EventType.mouseDrag || scrollOffset.y != 0))
      {
        if (scrollOffset.y == 0)
        {
          filesDic[file].offsetY -= Event.current.delta.y;
        }
        else
        {
          filesDic[file].offsetY -= scrollOffset.y;
          scrollOffset.y = 0;
        }
      }
      else if (!filesDic[file].drag)
      {
        filesDic[file].offsetY = Mathf.Lerp(filesDic[file].offsetY, 0, 0.1f);
      }
      if (Event.current.type == EventType.mouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
      {
        scrollOffset.y = 0;
        filesDic[file].drag = true;
      }
      else if (Event.current.type == EventType.mouseUp)
      {
        filesDic[file].drag = false;
      }
      return scrollOffset;
    }

    private void displayCraft(string file, craftObject craftInfo)
    {
      if (!toggleExtendedInfo.ContainsKey(file))
        toggleExtendedInfo.Add(file, false);
        GUILayout.BeginVertical(areaStyle);
        GUILayout.BeginHorizontal();
          if (GUILayout.Toggle(toggleExtendedInfo[file], new GUIContent("", "Show/hide extended info"), arrowStyle))
          {
            toggleExtendedInfo[file] = true;
          }
          else
          {
            toggleExtendedInfo[file] = false;
          }
          GUILayout.BeginVertical();
            Utilities.UI.createLabel(craftInfo.craftName, craftNameStyle);
          GUILayout.EndVertical();
          GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
              GUILayout.Space(25);
              createCraftLoadButton(file, craftInfo.craftComplete);
              createCraftEditCatsButton(file);
              if (!craftInfo.isHistoryFile)
              {
                string historyPath = createShowHistoryButton(file, craftInfo.craftName);
                createCraftDeleteButton(file, historyPath);
              }
              else
              {
                createCraftDeleteButton(file);
              }
            GUILayout.EndHorizontal();
          GUILayout.EndVertical();
        GUILayout.EndHorizontal();

      if (toggleExtendedInfo[file])
      {
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        Utilities.UI.createLabel(craftInfo.lastEdit.ToString("yyyy.MM.dd HH:mm:ss"), craftStyleShort);
        Utilities.UI.createLabel(Utilities.Craft.getPartAndStageString(craftInfo.partCount, "Part", false) + " in " + Utilities.Craft.getPartAndStageString(craftInfo.stageCount, "Stage"), craftStyleShort);
        Utilities.UI.createLabel("Craft cost: " + craftInfo.craftCost.ToString("N0"), craftStyleShort);
        if (!craftInfo.craftComplete)
          Utilities.UI.createLabel("Craft is missing Parts", textStyleRed);
        GUILayout.Space(5);
        GUILayout.EndVertical();
        GUILayout.BeginVertical();
        GUILayout.Label(new GUIContent(GetThumbnail(HighLogic.SaveFolder + "_" + currentEditor + "_" + craftInfo.craftName)), thumbnailStyle);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
      }

      GUILayout.EndVertical();
    }


    private string createShowHistoryButton(string file, string craftFileName)
    {
      string historyPath = currentSettings.getString("savePath") + craftFileName + "/";
      if (Utilities.UI.createButton("", buttonHistoryIconStyle, (!historyFilesDic.ContainsKey(file) || historyFilesDic[file].Length <= 0), "Show history of this craft"))
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
        GUI.BringWindowToFront(historyWindowID);
      }
      return historyPath;
    }

    private void createToggleAllButtons()
    {
      GUILayout.BeginVertical();
      GUILayout.Space(5);
      GUILayout.BeginHorizontal();
      Utilities.UI.createLabel("Show/hide all:", showHideAllTextStyle);
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
      if (Utilities.UI.createButton("", buttonDeleteIconStyle, "Delete this craft"))
      {
        if (File.Exists(file))
          File.Delete(file);
        toBeRemoved.AddUnique(file);
        if (!string.IsNullOrEmpty(filesDic[file].craftName))
        {
          deleteHistory(historyPath, file);
          if (currentSettings.getBool("deleteThumbnails"))
          {
            var thumbPath = KSPUtil.ApplicationRootPath + "/thumbs/" + HighLogic.SaveFolder + "_" + currentEditor + "_" + filesDic[file].craftName + ".png";
            if (File.Exists(thumbPath))
              File.Delete(thumbPath);
          }
        }
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
      if (Utilities.UI.createButton("", buttonLoadIconStyle, !craftComplete, "Load this craft"))
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

    private void createCraftEditCatsButton(string file)
    {
      if (Utilities.UI.createButton("", buttonEditCatIconStyle, "Edit this crafts categories"))
      {
        existingCraftCategories.Clear();
        foreach (var categoryPath in categories)
        {
          foreach (KeyValuePair<string, string> pair in categoryPath.Value)
          {
            if (pair.Value == file)
            {
              existingCraftCategories.Add(pair.Key);
            }
          }
        }
        existingCraftCategoriesFile = file;
      }
    }

    private void createHistoryWindow(int windowID)
    {
      GUILayout.BeginVertical();

      scrollPositionHistory = Utilities.UI.beginScrollView(scrollPositionHistory, 340, 445, false, true, GUIStyle.none, null, scrollview);
      foreach (string file in historyFilesDic[historyFiles])
      {
        if (!filesDic.ContainsKey(file))
        {
          continue;
        }
        var craftInfo = filesDic[file];
        if (!craftInfo.craftComplete && currentSettings.getBool("hideUnloadableCrafts"))
          continue;
        GUILayout.BeginVertical(areaStyle);
        GUILayout.BeginHorizontal();
        craftInfo.craftName = craftInfo.lastEdit.ToString("yyyy.MM.dd HH:mm:ss");
        displayCraft(file, craftInfo);
        GUILayout.BeginVertical();
        GUILayout.Space(0);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
      }
      GUILayout.EndScrollView();
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      if (Utilities.UI.createButton("Delete history", buttonStyle))
      {
        deleteHistory(showHistory, historyFiles);
      }
      if (Utilities.UI.createButton("Close", buttonStyle))
      {
        showHistory = null;
      }
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
      GUI.DragWindow();
      Utilities.UI.updateTooltipAndDrag();
    }
  }
}