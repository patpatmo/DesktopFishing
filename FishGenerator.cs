using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public string GenerateRandomItemWithImage()
        {
            // 生成随机物品类型
            ItemType itemType = GenerateRandomItem();
            // 获取对应类型的随机图片路径
            return GetRandomImagePath(itemType);
        }
        
    }
}