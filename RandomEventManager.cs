using System;
using System.Timers;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RandomFish
{
    // 事件类型枚举
    public enum Rarity
    {
        Common,     // 普通事件
        Rare        // 特殊事件
    }

    // 2. 具体事件（按不同稀缺性分别随机）
    public enum CommonEvent
    {
        FlyingFish,
        SharkFin,
        Floating,
        DolphinsJump,
        SeabirdsFlying
    }

    public enum RareEvent
    {
        LakeMonster,
        BigWhaleFountain,
        YellowDuck,
        S_Event
    }

    public class EventRandomizer
    {
        private static readonly Random _random = new Random();

        // 根据权重随机选择Rarity
        public static Rarity GetRandomRarity()
        {
            // 生成0-99之间的随机数
            int randomValue = _random.Next(100);

            // 90%概率返回Common，10%概率返回Rare
            if (randomValue < 90)
            {
                return Rarity.Common;
            }
            else
            {
                return Rarity.Rare;
            }
        }

        // 根据Rarity随机选择具体事件
        public static object GetRandomEventByRarity(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common:
                    // 获取所有CommonEvent值并随机选择一个
                    Array commonEvents = Enum.GetValues(typeof(CommonEvent));
                    return commonEvents.GetValue(_random.Next(commonEvents.Length));

                case Rarity.Rare:
                    // 获取所有RareEvent值并随机选择一个
                    Array rareEvents = Enum.GetValues(typeof(RareEvent));
                    return rareEvents.GetValue(_random.Next(rareEvents.Length));

                default:
                    throw new ArgumentException("未支持的Rarity类型: " + rarity);
            }
        }

        // 一键随机选择事件并执行相关函数
        public static void ExecuteRandomEvent()
        {
            Rarity rarity = GetRandomRarity();
            object eventObj = GetRandomEventByRarity(rarity);
            
            // 根据事件类型执行相应的函数
            switch (rarity)
            {
                case Rarity.Common:
                    CommonEvent commonEvent = (CommonEvent)eventObj;
                    ExecuteCommonEvent(commonEvent);
                    break;
                case Rarity.Rare:
                    RareEvent rareEvent = (RareEvent)eventObj;
                    ExecuteRareEvent(rareEvent);
                    break;
            }
        }

        // 执行普通事件的函数
        private static void ExecuteCommonEvent(CommonEvent commonEvent)
        {
            switch (commonEvent)
            {
                case CommonEvent.FlyingFish:
                    FlyingFish();
                    break;
                case CommonEvent.SharkFin:
                    SharkFin();
                    break;
                case CommonEvent.Floating:
                    Floating();
                    break;
                case CommonEvent.DolphinsJump:
                    DolphinsJump();
                    break;
                case CommonEvent.SeabirdsFlying:
                    SeabirdsFlying();
                    break;
            }
        }

        // 执行稀有事件的函数
        private static void ExecuteRareEvent(RareEvent rareEvent)
        {
            switch (rareEvent)
            {
                case RareEvent.LakeMonster:
                    LakeMonster();
                    break;
                case RareEvent.BigWhaleFountain:
                    BigWhaleFountain();
                    break;
                case RareEvent.YellowDuck:
                    YellowDuck();
                    break;
                case RareEvent.S_Event:
                    S_Event();
                    break;
            }
        }

        // 普通事件函数实现
        private static void FlyingFish()
        {
            Debug.WriteLine("普通事件：飞鱼跃出水面！");
            // 在这里添加飞鱼事件的具体实现
        }

        private static void SharkFin()
        {
            Debug.WriteLine("普通事件：发现鲨鱼鳍！");
            // 在这里添加鲨鱼鳍事件的具体实现
        }

        private static void Floating()
        {
            Debug.WriteLine("普通事件：有东西在漂浮！");
            // 在这里添加漂浮物事件的具体实现
        }

        private static void DolphinsJump()
        {
            Debug.WriteLine("普通事件：海豚跃出水面！");
            // 在这里添加海豚事件的具体实现
        }

        private static void SeabirdsFlying()
        {
            Debug.WriteLine("普通事件：海鸟在飞翔！");
            // 在这里添加海鸟事件的具体实现
        }

        // 稀有事件函数实现
        private static void LakeMonster()
        {
            Debug.WriteLine("稀有事件：湖怪出现了！");
            // 在这里添加湖怪事件的具体实现
        }

        private static void BigWhaleFountain()
        {
            Debug.WriteLine("稀有事件：大鲸鱼喷泉！");
            // 在这里添加大鲸鱼喷泉事件的具体实现
        }

        private static void YellowDuck()
        {
            Debug.WriteLine("稀有事件：黄色橡皮鸭！");
            // 在这里添加黄色橡皮鸭事件的具体实现
        }

        private static void S_Event()
        {
            Debug.WriteLine("稀有事件：S级事件！");
            // 在这里添加S级事件的具体实现
        }
    }
}