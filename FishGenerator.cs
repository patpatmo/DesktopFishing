using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using TransparentOverlay;

namespace RandomFish
{
    public enum ItemType
    {
        Fish,
        RareFish,
        Garbage,
        Collectible
    }
    public struct FishInfo
    {
        public ItemType type;
        public string imgPath;
    }

    public class FishGenerator
    {
        private static readonly Random _random = new Random();
        private const string AssetsBasePath = @"D:\Pratice\C++\FishingGame\DesktopFishing\Assets";

        private ItemType GenerateRandomItem()
        {
            int randomValue = _random.Next(100);

            if (randomValue < 50)        // 50%概率 普通鱼类
                return ItemType.Fish;
            else if (randomValue < 60)   // 10%概率 珍稀鱼类
                return ItemType.RareFish;
            else if (randomValue < 90)   // 30%概率 垃圾
                return ItemType.Garbage;
            else                        // 10%概率 收藏品
                return ItemType.Collectible;
        }
        private string GetRandomImagePath(ItemType itemType)
        {
            // 获取对应类型的文件夹路径
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string folderPath = Path.Combine(AssetsBasePath, itemType.ToString());

            // 检查文件夹是否存在
            if (!Directory.Exists(folderPath))
                return null;

            // 支持的图片扩展名
            string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".gif" };

            // 获取文件夹中所有图片文件
            var imageFiles = Directory.GetFiles(folderPath)
                .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();

            // 如果没有图片文件，返回null
            if (imageFiles.Count == 0)
                return null;

            // 随机选择一张图片并返回路径
            return imageFiles[_random.Next(imageFiles.Count)];
        }

        public FishInfo GenerateRandomItemWithImage()
        {
            // 生成随机物品类型
            ItemType itemType = GenerateRandomItem();
            // 获取对应类型的随机图片路径
            string path=GetRandomImagePath(itemType);
            FishInfo fishInfo = new FishInfo
            {
                type = itemType,
                imgPath = path
            };
            return fishInfo;
        }
        public static bool SetFolderIcon()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string folderPath = Path.Combine(desktopPath, "钓鱼桶");
            string icoPath = @"D:\Pratice\C++\FishingGame\DesktopFishing\Assets\ico\桶有鱼.ico";
            try
            {
                // 0. 确保ICO文件存在
                if (!File.Exists(icoPath)) return false;

                // 1. 创建文件夹（如果不存在）
                Directory.CreateDirectory(folderPath);

                // 2. 将ICO文件复制到目标文件夹（避免路径问题）
                string localIcoPath = Path.Combine(folderPath, "folder_icon.ico");
                File.Copy(icoPath, localIcoPath, overwrite: true);

                // 3. 写入desktop.ini
                string iniPath = Path.Combine(folderPath, "desktop.ini");
                File.WriteAllText(iniPath, 
                    "[.ShellClassInfo]\r\n" +
                    $"IconResource={Path.GetFileName(localIcoPath)},0");

                // 4. 设置文件属性
                File.SetAttributes(iniPath, 
                    FileAttributes.Hidden | FileAttributes.System);
                File.SetAttributes(folderPath, 
                    File.GetAttributes(folderPath) | FileAttributes.System);

                // 5. 强制刷新（关键！）
                UpdateFolderIconCache(folderPath);
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 强制刷新图标缓存（三种方法组合）
        private static void UpdateFolderIconCache(string folderPath)
        {
            // 方法1：修改文件夹时间戳
            Directory.SetLastWriteTime(folderPath, DateTime.Now);

            // 方法2：调用系统API
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

            // 方法3：发送WM_SETTINGCHANGE消息
            SendNotifyMessage(
                (IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE,
                (IntPtr)0, "Environment");
        }

    // Windows API 声明
    private const int HWND_BROADCAST = 0xFFFF;
    private const int WM_SETTINGCHANGE = 0x001A;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendNotifyMessage(IntPtr hWnd, int Msg, IntPtr wParam, string lParam);
    }
}