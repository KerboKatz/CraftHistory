using KerboKatz.UI;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace KerboKatz.CH
{
  public class CraftDirectoryData
  {
    internal string path;
    internal Transform uiObjectTransform;
    internal DirectoryInfo directoryInfo;
    internal InputField input;
    internal string name;
    internal GameObject gameObject;
    internal List<CraftDirectoryData> children = new List<CraftDirectoryData>();
    internal bool isVisible = true;
    internal UI.ContextMenu contextMenu;
    internal ContextMenuOption contextMenuPaste;

    internal void ToggleChildren(bool isOn)
    {
      isVisible = isOn;
      UpdateVisibility();
    }

    internal void UpdateVisibility(bool forceHide = false)
    {
      bool show = isVisible;
      if (forceHide)
        show = false;
      foreach (var child in children)
      {
        child.gameObject.SetActive(show);
        child.UpdateVisibility(!show);
      }
    }
  }
}