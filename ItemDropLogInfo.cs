using System;
using Terraria;
using TShockAPI;
using TShockAPI.Localization;

namespace ItemDropLog
{
	public struct ItemDropLogInfo
	{
		public DateTime Timestamp;

		public string ServerName;

		public string SourcePlayerName;

		public string SourceIP;

		public string TargetPlayerName;

		public string TargetIP;

		public string Action;

		public int ItemNetId;

		public string ItemName;

		public int ItemStack;

		public int ItemPrefix;

		public float DropX;

		public float DropY;

		public bool IsValid
		{
			get
			{
				return !string.IsNullOrEmpty(this.ServerName) && !string.IsNullOrEmpty(this.Action) && !string.IsNullOrEmpty(this.SourcePlayerName) && !string.IsNullOrEmpty(this.TargetPlayerName) && this.ItemNetId != 0;
			}
		}

		public ItemDropLogInfo(string action, string sourcePlayerName, string targetPlayerName, int itemNetId, int itemStack, int itemPrefix)
		{
			this = new ItemDropLogInfo(action, sourcePlayerName, targetPlayerName, itemNetId, itemStack, itemPrefix, 0f, 0f);
		}

		public ItemDropLogInfo(string action, string sourcePlayerName, string targetPlayerName, int itemNetId, int itemStack, int itemPrefix, float dropX, float dropY)
		{
			this.Timestamp = DateTime.Now;
			this.ServerName = TShock.Config.ServerName;
			this.SourcePlayerName = sourcePlayerName;
			this.SourceIP = string.Empty;
			this.TargetPlayerName = targetPlayerName;
			this.TargetIP = string.Empty;
			this.Action = action;
			this.ItemNetId = itemNetId;
			this.ItemName = string.Empty;
			this.ItemStack = itemStack;
			this.ItemPrefix = 0;
			this.DropX = dropX;
			this.DropY = dropY;
			if (itemNetId != 0)
			{
				this.ItemName = this.GetItemName(itemNetId);
			}
		}

		private string GetItemName(int netId)
		{
			Item itemById = TShock.Utils.GetItemById(netId);
			if (itemById != null && itemById.netID == netId)
			{
				return itemById.Name;
			}
			return string.Empty;
		}
	}
}
