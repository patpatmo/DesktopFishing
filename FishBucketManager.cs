using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

public static class FishBucketManager
{
    private static readonly string FishIconDir = @"D:\Pratice\C++\FishingGame\DesktopFishing\Assets\ico";
    private static readonly string BucketPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "钓鱼桶");

    /// <summary>
    /// 当钓到鱼时调用此方法
    /// </summary>
    /// <param name="fishName">鱼的名字（需要与ICO文件名一致）</param>
    public static void AddFishToBucket(string fishName)
    {
        string fileName = Path.GetFileNameWithoutExtension(fishName);
        try
        {
            // 确保钓鱼桶存在
            Directory.CreateDirectory(BucketPath);
            Debug.WriteLine(fileName);
            // 查找对应的鱼图标
            string icoPath = FindFishIcon(fileName);
            if (icoPath == null)
            {
                MessageBox.Show($"找不到{fileName}的图标", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 创建鱼的专属文件夹（按日期+鱼名）
            string fishFolderName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{fileName}";
            string fishFolderPath = Path.Combine(BucketPath, fishFolderName);
            Directory.CreateDirectory(fishFolderPath);

            // 设置文件夹图标
            SetFishFolderIcon(fishFolderPath, icoPath, fileName);

            // 写入捕获信息
            File.WriteAllText(
                Path.Combine(fishFolderPath, "捕获记录.txt"),
                $"捕获时间：{DateTime.Now}\n鱼类：{fileName}\n重量：{new Random().Next(1, 10)}kg");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FindFishIcon(string fishName)
    {
        string iconPath = Path.Combine(FishIconDir, fishName + ".ico");
        if (File.Exists(iconPath)) return iconPath;
        return null;
    }

    private static void SetFishFolderIcon(string folderPath, string iconPath, string fishName)
    {
        // 复制图标到目标文件夹
        string iconFileName = $"{fishName}_icon{Path.GetExtension(iconPath)}";
        string destIconPath = Path.Combine(folderPath, iconFileName);
        File.Copy(iconPath, destIconPath, overwrite: true);

        // 创建配置文件
        string iniContent = $"[.ShellClassInfo]\n" +
                          $"IconResource={iconFileName},0\n" +
                          $"InfoTip=捕获的{fishName}";
        
        File.WriteAllText(Path.Combine(folderPath, "desktop.ini"), iniContent);

        // 设置文件属性
        File.SetAttributes(destIconPath, FileAttributes.Hidden);
        File.SetAttributes(Path.Combine(folderPath, "desktop.ini"), 
            FileAttributes.Hidden | FileAttributes.System);
        File.SetAttributes(folderPath, 
            File.GetAttributes(folderPath) | FileAttributes.System);

        // 刷新图标
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}