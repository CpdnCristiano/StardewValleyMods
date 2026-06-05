using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        private static readonly Dictionary<string, string> _tvTipKeys = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            {
                "You may be used to this show airing on Mondays and Thursdays, but we are now airing on Tuesdays and Fridays as well! Tune in for brand new tips!",
                "tv.lotl.tip_0"
            },
            {
                "Did you know that we're not the only station with an extended schedule? Our friends at The Queen of Sauce are now airing an extra episode every Saturday!",
                "tv.lotl.tip_1"
            },
            {
                "Today we have a special guest in our studio! Please welcome... 'Rasmodius, Explorer of the Arcane'..? Sure thing buddy, the floor is yours!^^A very precious item was taken from me. It looks like ordinary ink, but it has power beyond your imagination. If you were to stumble upon it, please deliver it back to me. You will be generously rewarded. Make sure you speak to me directly!^^You heard the man folks, keep an eye out and you might earn something good!",
                "tv.lotl.tip_2"
            },
            {
                "A friend of mine once told me of a story. Late in fall, they turned on their TV, and it seemed to be... haunted? I'm not sure I believe such a fairytale, but I thought it was interesting. There's something sinister about the signals you can receive on there. But the guy is kind of a deadbeat, so he was probably just pulling my leg.",
                "tv.lotl.tip_3"
            },
            {
                "Some farmers enjoy going to the mines in their free time. That's fine, it's a good way to get ores and other valuable trinkets! Make sure you bring a weapon with you though, there are some nasty creatures down there. If you lost your weapon, check out your local adventure guild, I'm sure they'll be happy to help. You can even phone them! They do deliveries!",
                "tv.lotl.tip_4"
            },
            { "Animal or Human, everyone enjoys a good cuddle at night!", "tv.lotl.tip_5" },
            {
                "If you steal from your friends, they might resent you. But if you're close enough, usually they won't mind sharing some of their favorite items with you!",
                "tv.lotl.tip_6"
            },
            {
                "I heard a rumor going around that some magically-attuned people have invented a magical clock that can skip time ahead. That sounds crazy, but I'm a big fan of folklore!",
                "tv.lotl.tip_7"
            },
            {
                "There are legends among veteran fisherman, talking of fish that are stronger and rarer than all others. They claim there is only one of each, but that's just folklore. They are species like any other, don't overfish them!",
                "tv.lotl.tip_8"
            },
            {
                "We recently got a call-in from a viewer in a discordant land. Apparently, over there, they have a lot of support for budding farmers. If you can pin down where they are, you'll even find guides and other resources!",
                "tv.lotl.tip_9"
            },
            {
                "Sometimes, you might be a bit lost, and not know what is your purpose in life. Most cultures recommend looking to your ancestors for guidance. That's a bit old-fashioned, but sometimes an elder will have left you something that will help you figuring out your journey. Maybe a letter or something?",
                "tv.lotl.tip_10"
            },
            {
                "Ever wanna take a long nap and wake up to a bunch of days gone by? We've all been there. Just remember, while you're snoozin', your friends and pets might feel a mite neglected. Balance, that's the name of the game!",
                "tv.lotl.tip_11"
            },
            {
                "A backpack is a farmer's best friend. Out there somewhere, there's one for you too, just waitin' to be found. No fuss, no muss, just keep yours wits about ya, and maybe you'll stumble upon it!",
                "tv.lotl.tip_12"
            },
            {
                "The climate can be a bit unpredictable these days. Sometimes it can be hot for months in a row. Don't hesitate to make the most of it with regrowing crops!",
                "tv.lotl.tip_13"
            },
            {
                "With the climate anomalies happening recently, even I tend to get confused about what season we're in. Please forgive me if I get something wrong. These are unprecedented times after all!",
                "tv.lotl.tip_14"
            },
            {
                "Let's talk about crops. They don't all have the same value, but money is not everything, trust me. Even if you have access to more valuable crops, it's usually a good idea to plant a little bit of everything. You never know what you might find comes harvest day!",
                "tv.lotl.tip_15"
            },
            {
                "We all gotta make a living somehow. Some people opt to gather interesting stuff, and move from town to town to sell it. They can provide out of season seeds, crops and fish. I hear some even use metal detectors to find and sell minerals and artifacts. How crazy is that?",
                "tv.lotl.tip_16"
            },
            {
                "Being friendly is generally a good thing, but you don't want to be taken advantage of either. Don't bother being too much more friendly to someone who isn't reciprocating yet. Give them time, I'm sure you'll be best buddies in a jiffy!",
                "tv.lotl.tip_17"
            },
            {
                "Most bars have old-timey video games available in them. These old games often contain cheat codes, but can only be used once you beat the game once. So you don't need to go back to do everything you missed, a simple command will do the trick!",
                "tv.lotl.tip_18"
            },
            {
                "Don't forget to never judge a book by its cover. Sometimes, even the most unassuming of doors can hide an extremely valuable interior. It's always worth a knock!",
                "tv.lotl.tip_19"
            },
            {
                "I heard a new show started airing recently. Tune in on Mondays and Fridays for... 'The Gateway Gazette'? I wonder what that's about...",
                "tv.lotl.tip_20"
            },
            {
                "Life offers many doors, but some days, you might walk into one, and it's just a closet. Don't sweat it! Self care sometimes means going back to bed and calling it a day.",
                "tv.lotl.tip_21"
            },
            {
                "Farmers don't always have it easy, so we need to stick together! Sending a gift to a farmer, even one far away, can do wonders to build community and make their day better! Just remember there's a fee. Worth it for the smiles it brings!",
                "tv.lotl.tip_22"
            },
            {
                "If you have a friend who needs to do a lot of running around, you can give them a coffee. I'm sure they'll appreciate the speed boost!",
                "tv.lotl.tip_23"
            },
            {
                "Opening your mail is usually pleasant, but sometimes, life throws you a curveball. Those unexpected surprises ain't nothin' to lose sleep over. They're just a little hiccup, no big deal. Keep smilin'!",
                "tv.lotl.tip_24"
            },
            {
                "We have had reports of mail letters across the countryside being delivered with nasty surprises in them. Don't let 'em ruffle your feathers! Stay sharp and find a way to turn the tables. Remember, adversity's the best teacher on the rocky road of life!",
                "tv.lotl.tip_25"
            },
            {
                "I've heard a good story the other day. Apparently, there's a white-haired woman hiding in Cindersap forest. She only comes out at night, and it seems she's quite the troublemaker. But I'm sure that's just a myth to scare the kiddos into going to bed!",
                "tv.lotl.tip_26"
            },
            {
                "Brave adventurers are always talking about myths and legends. Rumor has it that, if you go deep into the woods, and you're lucky enough, you might encounter magical beings. The experience of petting a unicorn is unrivaled! But also, some people just dump their trash there. Try your luck!",
                "tv.lotl.tip_27"
            },
            {
                "We got a letter from a folk all the way back in {2}. They recommend completing a... '{0}'?. I don't know what that is, but apparently, it's great! You should really get on that!",
                "tv.lotl.tip_28"
            },
            {
                "...and that folks is how an ol' goblin changed my friend's life around.  Who knew a crayfish dish would be the thing to do it!  I say pay it forward.  Who knows, even goblins might teach ya a thing or two!",
                "tv.lotl.tip_29"
            },
            {
                "Now here's an odd rumor from an ol' miss up in Grampleton.  Mystics capable of turning the weave so thoroughly you can even hear their whispers over the radio!  Might help in a pinch I say!",
                "tv.lotl.tip_30"
            },
        };

        private static readonly Dictionary<string, string> _goalKeys = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            // GetGoalString()
            {
                "Complete Grandpa's Evaluation with a score of at least 12 (4 candles)",
                "goal.grandpa_evaluation"
            },
            { "Reach Floor 120 in the Pelican Town Mineshaft", "goal.bottom_of_mines" },
            { "Complete the Community Center", "goal.community_center" },
            { "Find Secret Note #10 and complete the \"Cryptic Note\" Quest", "goal.cryptic_note" },
            { "Catch every single one of the 55 fish available in the game", "goal.master_angler" },
            {
                "Complete the Museum Collection by donating all 95 items",
                "goal.complete_collection"
            },
            { "Get married and have two children", "goal.full_house" },
            { "Find all 130 Golden Walnuts", "goal.greatest_walnut_hunter" },
            { "Complete all the monster slaying goals", "goal.protector_of_the_valley" },
            { "Ship every item", "goal.full_shipment" },
            { "Cook every recipe", "goal.gourmet_chef" },
            { "Craft every item", "goal.craft_master" },
            { "Earn 10 000 000g", "goal.legend" },
            { "Obtain all stardrops", "goal.mystery_of_the_stardrops" },
            { "Wear every Hat", "goal.mad_hatter" },
            { "Eat every item", "goal.ultimate_foodie" },
            { "Complete every Archipelago check", "goal.allsanity" },
            { "Achieve Perfection", "goal.perfection" },
            // GetGoalStringGrandpa()
            { "Make the most of this farm, and make me proud", "grandpa.grandpa_evaluation" },
            { "Finish exploring the mineshaft in this town for me", "grandpa.bottom_of_mines" },
            {
                "Restore the old Community Center for the sake of all the villagers",
                "grandpa.community_center"
            },
            {
                "Meet an old friend of mine on floor 100 of the Skull Cavern",
                "grandpa.cryptic_note"
            },
            {
                "Catch and document every specie of fish in the Ferngill Republic",
                "grandpa.master_angler"
            },
            {
                "Restore our beautiful museum with a full collection of various artifacts and minerals",
                "grandpa.complete_collection"
            },
            {
                "I wish for my bloodline to thrive. Please find a partner and live happily ever after",
                "grandpa.full_house"
            },
            {
                "Prove your worth to an old friend of mine, and become the greatest walnut hunter",
                "grandpa.greatest_walnut_hunter"
            },
            {
                "Make sure the valley is safe for generations to come. Rip and tear, until it is done",
                "grandpa.protector_of_the_valley"
            },
            {
                "Contribute to the local economy and market, by shipping as many things as you can",
                "grandpa.full_shipment"
            },
            {
                "Become a world-class chef, learn and cook all the recipes you can find",
                "grandpa.gourmet_chef"
            },
            {
                "Get used to making things with your hands, and craft as many items as you can",
                "grandpa.craft_master"
            },
            {
                "Nothing beats cold hard cash. Become rich enough, and buy your happiness",
                "grandpa.legend"
            },
            {
                "A healthy body is a healthy mind. Get in shape by increasing your energy to the maximum.",
                "grandpa.mystery_of_the_stardrops"
            },
            {
                "In life, it's important to be able to wear many hats. Trust no one, and wear all of them!",
                "grandpa.mad_hatter"
            },
            {
                "Learn to enjoy all the good things in life, and taste a little bit of everything you can find!",
                "grandpa.ultimate_foodie"
            },
            {
                "You cannot leave anyone stranded in a Burger King. Leave no loose ends",
                "grandpa.allsanity"
            },
            {
                "For a fulfilling life, you need to do a lot of everything. Leave no loose ends",
                "grandpa.perfection"
            },
        };

        public static string GetLocalizedTVTip(string englishTip)
        {
            if (_tvTipKeys.TryGetValue(englishTip, out var key))
            {
                if (ModEntry.Translation.ContainsKey(key))
                {
                    return ModEntry.Translation.Get(key).ToString();
                }
            }
            return englishTip;
        }

        public static string GetLocalizedGoalString(string englishGoal)
        {
            if (_goalKeys.TryGetValue(englishGoal, out var key))
            {
                if (ModEntry.Translation.ContainsKey(key))
                {
                    return ModEntry.Translation.Get(key).ToString();
                }
            }
            return englishGoal;
        }
    }
}
