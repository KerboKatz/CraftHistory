using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KerboKatz
{
  class craftObject
  {
    public string craftName;
    public DateTime lastEdit;
    public int partCount;
    public int stageCount;
    public float craftCost;
    public bool craftComplete;
    public string[] craftCategories;
    public bool isHistoryFile;
    public bool overwriteExisting;
    public craftObject(string craftName, DateTime lastEdit, int partCount, int stageCount, float craftCost, bool craftComplete, string[] craftCategories=null, bool isHistoryFile = false, bool overwriteExisting=false)
    {
      this.craftName = craftName;//1
      this.lastEdit = lastEdit;
      this.partCount = partCount;//3
      this.stageCount = stageCount;
      this.craftCost = craftCost;//5
      this.craftComplete = craftComplete;
      this.craftCategories = craftCategories;//7
      this.isHistoryFile = isHistoryFile;
      this.overwriteExisting = overwriteExisting;//9
    }
  }
}
