using System;
using System.Data;
using System.Threading.Tasks;
using TShockAPI;
using TShockAPI.DB;

namespace ItemDropLog
{
	public class ItemDropLogger
	{
        //private readonly IDbConnection _db;
        private static object syncLock = new object();

        /*
        internal async Task<int> Query(string query, params object[] args)
        {
            return await Task.Run(() =>
            {
                try
                {
                    lock (syncLock)
                    {
                        return _db.Query(query, args);
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.Error(ex.ToString());
                    return 0;
                }
            });
        }*/

        internal async void CreateEntry(ItemDropLogInfo info)
        {
            await Task.Run(() =>
            {
                try
                {
                    lock (syncLock)
                    {
                        if (!info.IsValid)
                        {
                            TShock.Log.ConsoleError("ItemDropLogger tried to create an entry based on invalid info.");
                            return;
                        }
                        //ItemDropLogPlugin.db.Query
                        TSPlayer.All.SendInfoMessage("[ItemLog] Tried query 'CreateEntry'");
                        ItemDropLogPlugin.db.Query("INSERT INTO `ItemLog` (`Timestamp`,`ServerName`,`SourcePlayerName`,`SourceIP`,`TargetPlayerName`,`TargetIP`,`Action`,`DropX`,`DropY`,`ItemNetId`,`ItemName`,`ItemStack`,`ItemPrefix`) VALUES (@0,@1,@2,@3,@4,@5,@6,@7,@8,@9,@10,@11,@12)", new object[]
                        {
                            info.Timestamp.ToString("s"),
                            info.ServerName,
                            info.SourcePlayerName,
                            info.SourceIP,
                            info.TargetPlayerName,
                            info.TargetIP,
                            info.Action,
                            info.DropX,
                            info.DropY,
                            info.ItemNetId,
                            info.ItemName,
                            info.ItemStack,
                            info.ItemPrefix
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.Error(ex.ToString());
                    return;
                }
            });

		}

		internal async void CreateItemEntry(ItemDropLogInfo info)
		{
            await Task.Run(() =>
            {
                try
                {
                    lock (syncLock)
                    {
                        TSPlayer.All.SendInfoMessage("[ItemLog] Tried query 'CreateItemEntry'");
                        ItemDropLogPlugin.db.Query("INSERT INTO `ItemLog` (`Timestamp`,`ServerName`,`SourcePlayerName`,`SourceIP`,`Action`,`DropX`,`DropY`,`ItemNetId`,`ItemName`,`ItemStack`,`ItemPrefix`) VALUES (@0,@1,@2,@3,@4,@5,@6,@7,@8,@9,@10)", new object[]
                            {
                                info.Timestamp.ToString("s"),
                                info.ServerName,
                                info.SourcePlayerName,
                                info.SourceIP,
                                info.Action,
                                info.DropX,
                                info.DropY,
                                info.ItemNetId,
                                info.ItemName,
                                info.ItemStack,
                                info.ItemPrefix
                            });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.Error(ex.ToString());
                    return;
                }
            });
		}

		internal async void UpdateItemEntry(ItemDropLogInfo info)
		{
            await Task.Run(() =>
            {
                try
                {
                    lock (syncLock)
                    {
                        TSPlayer.All.SendInfoMessage("[ItemLog] Tried query 'UpdateItemEntry'");
                        ItemDropLogPlugin.db.Query("UPDATE `ItemLog` SET `TargetPlayerName`=@0, `TargetIP`=@1, `Action`=@2 WHERE `ServerName`=@3 AND `Action`=@4 AND `SourcePlayerName`=@5 AND `ItemNetId`=@6 AND `ItemStack`=@7 AND `ItemPrefix`=@8", new object[]
                            {
                                info.TargetPlayerName,
                                info.TargetIP,
                                info.Action,
                                info.ServerName,
                                "PlayerDrop",
                                info.SourcePlayerName,
                                info.ItemNetId,
                                info.ItemStack,
                                info.ItemPrefix
                            });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.Error(ex.ToString());
                    return;
                }
            });
        }
	}
}
