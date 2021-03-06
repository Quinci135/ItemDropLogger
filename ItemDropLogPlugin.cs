using Microsoft.Xna.Framework;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Localization;

namespace ItemDropLog
{
	[ApiVersion(2, 1)]
	public class ItemDropLogPlugin : TerrariaPlugin
	{
		private static readonly string ConfigPath = Path.Combine(TShock.SavePath, "itemdroplog.json");

		private ItemDrop[] _drops;

        private ItemDropLogger logger = new ItemDropLogger();

		private IList<ItemDrop> _playerDropsPending;

		private IList<Item> _ignoredItems;

        public static IDbConnection db;


		public override string Author
		{
			get
			{
				return "Originally from PhoenixICE & reinovated by Hiarni";
			}
		}

		public override string Name
		{
			get
			{
				return "Item Drop Logger";
			}
		}

		public override string Description
		{
			get
			{
				return "Item Drop Logger";
			}
		}

		public override Version Version
		{
			get
			{
				return new Version(1, 2, 0);
			}
		}

		public string SavePath
		{
			get
			{
				return TShock.SavePath;
			}
		}


		public ItemDropLogPlugin(Main game) : base(game)
		{
			this._drops = new ItemDrop[Main.item.Length];
			this._playerDropsPending = new List<ItemDrop>(Main.item.Length);
			this._ignoredItems = new List<Item>();
		}

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData);
			ServerApi.Hooks.NetSendData.Register(this, OnSendData);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
                db.Dispose();
			}
			base.Dispose(disposing);
		}

		private void OnInitialize(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command("droplog.search", PlayerItemHistoryReceive, "lr") { AllowServer = true, HelpText = "Check what items the target player has received"});
			Commands.ChatCommands.Add(new Command("droplog.search", PlayerItemHistoryGive, "lg") { AllowServer = true, HelpText = "Check what items the target player has given."});
			Commands.ChatCommands.Add(new Command("droplog.reload", PlayerItemHistoryReload, "lreload") { AllowServer = true, HelpText = "Reloads the itemdroplog configuration file." });
			Commands.ChatCommands.Add(new Command("droplog.flush", PlayerItemHistoryFlush, "lflush") { AllowServer = true, HelpText = "Clears the itemdrop logs." });
            Commands.ChatCommands.Add(new Command("droplog.list", ListIgnoredItems, "li") { AllowServer = true, HelpText = "Lists items that do not get logged." });

			switch (TShock.Config.StorageType.ToLower())
			{
				case "mysql":
						string[] host = TShock.Config.MySqlHost.Split(':');
                        db = new MySqlConnection()
						{
							ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
									host[0],
									host.Length == 1 ? "3306" : host[1],
									TShock.Config.MySqlDbName,
									TShock.Config.MySqlUsername,
									TShock.Config.MySqlPassword)
						};
						break;
				case "sqlite":
					string sql = Path.Combine(TShock.SavePath, "ItemLog.sqlite");
                    db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
					break;
					
			}
            SqlTableCreator sqlcreator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
			sqlcreator.EnsureTableStructure(new SqlTable("ItemLog",
									new SqlColumn("ID", MySqlDbType.Int32) { Unique = true, Primary = true, AutoIncrement = true },
									new SqlColumn("Timestamp", MySqlDbType.String, 19),
									new SqlColumn("ServerName", MySqlDbType.String, 64),
									new SqlColumn("SourcePlayerName", MySqlDbType.String, 30),
									new SqlColumn("SourceIP", MySqlDbType.String, 16),
									new SqlColumn("TargetPlayerName", MySqlDbType.String, 30),
									new SqlColumn("TargetIP", MySqlDbType.String, 16),
									new SqlColumn("Action", MySqlDbType.String, 16),
									new SqlColumn("DropX", MySqlDbType.Int32),
									new SqlColumn("DropY", MySqlDbType.Int32),
									new SqlColumn("ItemNetId", MySqlDbType.Int32),
									new SqlColumn("ItemName", MySqlDbType.String, 70),
									new SqlColumn("ItemStack", MySqlDbType.Int32),
									new SqlColumn("ItemPrefix", MySqlDbType.Int32)
									));
		}

		private void OnPostInitialize(EventArgs args)
		{
			this.SetupConfig();
		}

		private void OnGetData(GetDataEventArgs args)
		{
			if ((int)args.MsgID == (int)PacketTypes.ItemDrop)
			{
				TSPlayer tSPlayer = TShock.Players[args.Msg.whoAmI];
				using (MemoryStream memoryStream = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
				{
					using (BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, true))
					{
						int num = (int)binaryReader.ReadInt16();
						float num2 = binaryReader.ReadSingle();
						float num3 = binaryReader.ReadSingle();
						binaryReader.ReadSingle();
						binaryReader.ReadSingle();
						int num4 = (int)binaryReader.ReadInt16();
						int num5 = (int)binaryReader.ReadByte();
						binaryReader.ReadBoolean();
						int num6 = (int)binaryReader.ReadInt16();
						if (num == 400)
						{
							Item itemById = TShock.Utils.GetItemById(num6);
							string name = tSPlayer.Name;

                            string sourceIP = tSPlayer.IP.Split(new char[]
							{
								':'
							})[0];
							float dropX = num2 / 16f;
							float dropY = num3 / 16f;
							this._playerDropsPending.Add(new ItemDrop(name, itemById.netID, num4, (int)num5, dropX, dropY));
							if (this.CheckItem(itemById))
							{
								logger.CreateItemEntry(new ItemDropLogInfo("PlayerDrop", name, string.Empty, itemById.netID, num4, num5, dropX, dropY)
								{
									SourceIP = sourceIP
								});
							}
						}
						if (num < 400 && num6 == 0)
						{
							Item item = Main.item[num];
							if (item.netID != 0)
							{
								string name2 = tSPlayer.Name;
                                string targetIP = tSPlayer.IP.Split(new char[]
								{
									':'
								})[0];
								ItemDrop itemDrop = this._drops[num];
								if (this._drops[num] != null && this._drops[num].NetworkId != 0)
								{
									if (this.CheckItem(item))
									{
										logger.UpdateItemEntry(new ItemDropLogInfo("Pickup", itemDrop.SourceName, name2, itemDrop.NetworkId, itemDrop.Stack, (int)itemDrop.Prefix)
										{
											TargetIP = targetIP
										});
									}
									this._drops[num] = null;
								}
							}
						}
					}
				}
			}
		}

		private void OnSendData(SendDataEventArgs args)
		{
			if (args.MsgId != PacketTypes.ItemDrop)
			{
				return;
			}
			int number = args.number;
			if (this._playerDropsPending.Count > 0 && number < 400)
			{
				Item item = Main.item[number];
				ItemDrop itemDrop = this._playerDropsPending.FirstOrDefault((ItemDrop x) => x.NetworkId == item.netID && x.Stack == item.stack && x.Prefix == item.prefix);
				if (itemDrop != null)
				{
					this._drops[number] = itemDrop;
					this._playerDropsPending.Remove(itemDrop);
				}
			}
		}

		private void PlayerItemHistoryReceive(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /lr <player> [page] [item id/name]");
				return;
			}
			string plr = args.Parameters[0];
            List<TSPlayer> list = TShockAPI.TSPlayer.FindByNameOrID(plr);
            TShockAPI.DB.UserAccount user = TShock.UserAccounts.GetUserAccountByName(plr);
            string text2;
			if (list.Count == 0)
			{
                using (QueryResult queryResult = db.QueryReader("SELECT COUNT(*) AS `Count` FROM `ItemLog` WHERE `TargetPlayerName`=@0", plr))
				{
					if (!queryResult.Read() || queryResult.Get<int>("Count") <= 0)
					{
						args.Player.SendErrorMessage("Invalid player!");
						return;
					}
				}
				text2 = plr;
			}

            if (list.Count <= 1)
			{
                text2 = user.Name;
            }
			else
			{
				args.Player.SendMultipleMatchError( from p in list select p.Name);
				return;
			}
			int num;
			if (args.Parameters.Count < 2 || !int.TryParse(args.Parameters[1], out num) || num < 0)
			{
				num = 1;
			}
			Item item = null;
			if (args.Parameters.Count >= 3)
			{
				List<Item> itemByIdOrName = TShock.Utils.GetItemByIdOrName(args.Parameters[2]);
				if (itemByIdOrName.Count == 0)
				{
					args.Player.SendErrorMessage("Invalid item!");
					return;
				}
				if (itemByIdOrName.Count > 1)
				{
					args.Player.SendMultipleMatchError( from x in itemByIdOrName
					select x.Name);
					return;
				}
				item = itemByIdOrName[0];
			}
			QueryResult queryResult2;
			if (item != null)
			{
                queryResult2 = db.QueryReader("SELECT * FROM `ItemLog` WHERE `TargetPlayerName`=@0 AND `ItemNetId`=@1 ORDER BY `Timestamp` DESC LIMIT @2,@3", text2, item.netID, (num - 1) * 10, 10);
			}
			else
			{
                queryResult2 = db.QueryReader("SELECT * FROM `ItemLog` WHERE `TargetPlayerName`=@0 ORDER BY `Timestamp` DESC LIMIT @1,@2", text2, (num - 1) * 10, 10);
			}   
			using (queryResult2)
			{
				args.Player.SendInfoMessage("Results for {0}:", text2);
				int num2 = (num - 1) * 5;
				DateTime now = DateTime.Now;
				while (queryResult2.Read())
				{
					Item itemById = TShock.Utils.GetItemById(queryResult2.Get<int>("ItemNetId"));
					string s = queryResult2.Get<string>("Timestamp");
					string text3 = queryResult2.Get<string>("ServerName");
					string text4 = queryResult2.Get<string>("SourcePlayerName");
					string text5 = queryResult2.Get<string>("TargetPlayerName");
					string value = queryResult2.Get<string>("ItemName");
					int num3 = queryResult2.Get<int>("ItemStack");
					int text6 = queryResult2.Get<int>("ItemPrefix");
					StringBuilder stringBuilder = new StringBuilder();
					stringBuilder.Append(num3).Append(' ');
					if (text6 != 0)
					{
						stringBuilder.Append(GetPrefixName(text6)).Append(' ');
					}
					stringBuilder.Append(value); 
					if (itemById.maxStack > 1)
					{
						stringBuilder.Append(' ').AppendFormat("({0}/{1})", num3, itemById.maxStack);
					}
					string text7 = string.Empty;
					if (!string.IsNullOrEmpty(text3))
					{
						text7 = " on " + text3;
					}
					DateTime d = DateTime.Parse(s);
					TimeSpan span = now - d;
					args.Player.SendInfoMessage("{0}. {1} received {2} from {3}{4} ({5} ago)", ++num2,text5, stringBuilder.ToString(), text4, text7, this.TimeSpanToDurationString(span));
				}
			}
		}

		private void PlayerItemHistoryGive(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /lg <player> [page] [item id/name]");
				return;
			}
			string text = args.Parameters[0];
			List<TSPlayer> list = TShockAPI.TSPlayer.FindByNameOrID(text);
            TShockAPI.DB.UserAccount user = TShock.UserAccounts.GetUserAccountByName(text);
            string text2;
			if (list.Count == 0)
			{
                using (QueryResult queryResult = db.QueryReader("SELECT COUNT(*) AS `Count` FROM `ItemLog` WHERE `SourcePlayerName`=@0", text))
				{
					if (!queryResult.Read() || queryResult.Get<int>("Count") <= 0)
					{
						args.Player.SendErrorMessage("Invalid player!");
						return;
					}
				}
				text2 = text;
			}
			if (list.Count <= 1)
			{
                text2 = user.Name;
			}
			else
			{
				args.Player.SendMultipleMatchError( from p in list select p.Name);
				return;
			}
			int num;
			if (args.Parameters.Count < 2 || !int.TryParse(args.Parameters[1], out num) || num < 0)
			{
				num = 1;
			}
			Item item = null;
			if (args.Parameters.Count >= 3)
			{
				List<Item> itemByIdOrName = TShock.Utils.GetItemByIdOrName(args.Parameters[2]);
				if (itemByIdOrName.Count == 0)
				{
					args.Player.SendErrorMessage("Invalid item!");
					return;
				}
				if (itemByIdOrName.Count > 1)
				{
                    args.Player.SendMultipleMatchError( from x in itemByIdOrName
					select x.Name);
					return;
				}
				item = itemByIdOrName[0];
			}
			QueryResult queryResult2;
			if (item != null)
			{
                queryResult2 = db.QueryReader("SELECT * FROM `ItemLog` WHERE `SourcePlayerName`=@0 AND `ItemNetId`=@1 ORDER BY `Timestamp` DESC LIMIT @2,@3", text2, item.netID, (num - 1) * 10, 10);
			}
			else
			{
                queryResult2 = db.QueryReader("SELECT * FROM `ItemLog` WHERE `SourcePlayerName`=@0 ORDER BY `Timestamp` DESC LIMIT @1,@2", text2, (num - 1) * 10, 10);
			}
			using (queryResult2)
			{
				args.Player.SendInfoMessage("Results for {0}:", text2);
				int num2 = (num - 1) * 5;
				DateTime now = DateTime.Now;
				while (queryResult2.Read())
				{
					Item itemById = TShock.Utils.GetItemById(queryResult2.Get<int>("ItemNetId"));
					string s = queryResult2.Get<string>("Timestamp");
					string text3 = queryResult2.Get<string>("ServerName");
					string text4 = queryResult2.Get<string>("SourcePlayerName");
					string text5 = queryResult2.Get<string>("TargetPlayerName");
					string value = queryResult2.Get<string>("ItemName");
					int num3 = queryResult2.Get<int>("ItemStack");
					int text6 = queryResult2.Get<int>("ItemPrefix");
					StringBuilder stringBuilder = new StringBuilder();
					stringBuilder.Append(num3).Append(' ');
					//Prefix should be working
					if (text6 != 0)
					{
						stringBuilder.Append(GetPrefixName(text6)).Append(' ');
					}
					stringBuilder.Append(value);
					if (itemById.maxStack > 1)
					{
						stringBuilder.Append(' ').AppendFormat("({0}/{1})", num3, itemById.maxStack);
					}
					string text7 = string.Empty;
					if (!string.IsNullOrEmpty(text3))
					{
						text7 = " on " + text3;
					}
					DateTime d = DateTime.Parse(s);
					TimeSpan span = now - d;
					args.Player.SendInfoMessage("{0}. {1} gave {2} to {3}{4} ({5} ago)", ++num2, text4, stringBuilder.ToString(), text5, text7, this.TimeSpanToDurationString(span));
				}
			}
		}

		private void PlayerItemHistoryReload(CommandArgs args)
		{
			this.LoadConfig(ConfigPath);
			args.Player.SendInfoMessage("ItemDropLog config reloaded.");
		}

		private void PlayerItemHistoryFlush(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /lflush <days>");
				return;
			}
			int num;
			if (!int.TryParse(args.Parameters[0], out num) || num < 1)
			{
				args.Player.SendErrorMessage("Invalid days");
				return;
			}
			DateTime dateTime = DateTime.Now.AddDays((double)(-(double)num));
            int num2 = db.Query("DELETE FROM `ItemLog` WHERE `Timestamp`<@0 AND `ServerName`=@1", new object[]
			{
				dateTime.ToString("s"),
				TShock.Config.ServerName
			});
			args.Player.SendInfoMessage("Successfully flushed {0:n0} rows from the database.", new object[]
			{
				num2
			});
		}

		private string TimeSpanToDurationString(TimeSpan span)
		{
			int days = span.Days;
			int hours = span.Hours;
			int minutes = span.Minutes;
			int seconds = span.Seconds;
			List<string> list = new List<string>(4);
			if (days > 0)
			{
				list.Add(days + "d");
			}
			if (hours > 0)
			{
				list.Add(hours + "h");
			}
			if (minutes > 0)
			{
				list.Add(minutes + "m");
			}
			if (seconds > 0)
			{
				list.Add(seconds + "s");
			}
			return string.Join(" ", list);
		}

        private void ListIgnoredItems(CommandArgs args)
        {
            args.Player.SendMessage("The following drops are not logged:", Color.BlanchedAlmond);
            string ignoredItems = this._ignoredItems[0].Name;
            for (int i = 1; i < this._ignoredItems.Count; i++)
            {
                ignoredItems += $", {this._ignoredItems[i].Name}";
            }
            args.Player.SendInfoMessage(ignoredItems + ".");
        }

        private string GetPrefixName(int pre)
		{
			string result = "None";
			if (pre > 0)
			{
                result = EnglishLanguage.GetPrefixById(pre);
			}
			return result;
		}

		private void SetupConfig()
		{
			try
			{
				if (File.Exists(ConfigPath))
				{
					this.LoadConfig(ConfigPath);
				}
				else
				{
					TShock.Log.ConsoleError("ItemDropLog configuration not found. Using default configuration.");
					this.LoadConfig(null);
					Config.SaveInstance(ConfigPath);
				}
			}
			catch (Exception ex)
			{
				TShock.Log.ConsoleError(ex.ToString());
			}
		}

		private void LoadConfig(string path)
		{
			Config.CreateInstance(path);
			this._ignoredItems.Clear();
			foreach (string current in Config.Instance.IgnoredItems)
			{
				List<Item> itemByIdOrName = TShock.Utils.GetItemByIdOrName(current);
				if (itemByIdOrName.Count > 0)
				{
					this._ignoredItems.Add(itemByIdOrName[0]);
				}
			}
		}

		private bool CheckItem(Item item)
		{
			return this._ignoredItems.All((Item x) => x.netID != item.netID);
		}
	}
}
