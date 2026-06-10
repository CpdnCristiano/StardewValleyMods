using System;
using System.Numerics;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class BankHandlerPatcher
    {
        private const string CommandPrefix = "!!";
        private const string BankCommand = "bank";
        private const string BankingTeamKey = "EnergyLink{0}";
        private const int MinOperationAmount = 1;
        private const int MaxOperationAmount = int.MaxValue - 2;
        private const double BankTax = 0.25;

        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StardewArchipelago.Archipelago.BankHandler");
                if (targetType == null)
                {
                    return;
                }

                PatchMethod(harmony, targetType, "HandleBankCommand", nameof(HandleBankCommand_Prefix));
                PatchMethod(harmony, targetType, "HandleResetCommand", nameof(HandleResetCommand_Prefix));
                PatchMethod(harmony, targetType, "HandleDepositCommand", nameof(HandleDepositCommand_Prefix));
                PatchMethod(harmony, targetType, "HandleWithdrawCommand", nameof(HandleWithdrawCommand_Prefix));
                PatchMethod(
                    harmony,
                    AccessTools.Method(targetType, "PrintCurrentBalance", new[] { typeof(BigInteger) }),
                    nameof(PrintCurrentBalance_Prefix)
                );
                PatchMethod(
                    harmony,
                    AccessTools.Method(targetType, "PrintUsageRules"),
                    nameof(PrintUsageRules_Prefix)
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed to patch BankHandler: {ex}", LogLevel.Error);
            }
        }

        private static void PatchMethod(Harmony harmony, Type targetType, string methodName, string prefixName)
        {
            var method = AccessTools.Method(targetType, methodName);
            PatchMethod(harmony, method, prefixName);
        }

        private static void PatchMethod(Harmony harmony, System.Reflection.MethodBase method, string prefixName)
        {
            if (method == null)
            {
                return;
            }

            harmony.Patch(
                original: method,
                prefix: new HarmonyMethod(typeof(BankHandlerPatcher), prefixName)
            );
        }

        public static bool HandleBankCommand_Prefix(object __instance, string message, ref bool __result)
        {
            try
            {
                var archipelago = GetMemberValue<object>(__instance, "_archipelago");
                if (archipelago == null)
                {
                    __result = false;
                    return false;
                }

                var bankPrefix = $"{CommandPrefix}{BankCommand}";
                if (!message.StartsWith(bankPrefix, StringComparison.Ordinal))
                {
                    __result = false;
                    return false;
                }

                if (!GetSlotDataBool(archipelago, "Banking") || !InvokeMethod<bool>(archipelago, "MakeSureConnected"))
                {
                    Game1.chatBox?.addMessage(ModEntry.Translation.Get("bank.no_account").ToString(), Color.Gold);
                    __result = true;
                    return false;
                }

                var bankCommand = message.Substring(bankPrefix.Length).Trim();
                if (string.IsNullOrWhiteSpace(bankCommand))
                {
                    InvokeMethod(__instance, "PrintCurrentBalance");
                    __result = true;
                    return false;
                }

                var bankCommandParts = bankCommand.Split(" ");
                if (bankCommandParts.Length != 2)
                {
                    InvokeStaticMethod(__instance.GetType(), "PrintUsageRules");
                    __result = true;
                    return false;
                }

                if (bankCommandParts[0].StartsWith("D", StringComparison.OrdinalIgnoreCase))
                {
                    InvokeMethod(__instance, "HandleDepositCommand", bankCommandParts[1]);
                    __result = true;
                    return false;
                }

                if (bankCommandParts[0].StartsWith("W", StringComparison.OrdinalIgnoreCase))
                {
                    InvokeMethod(__instance, "HandleWithdrawCommand", bankCommandParts[1]);
                    __result = true;
                    return false;
                }

                InvokeStaticMethod(__instance.GetType(), "PrintUsageRules");
                __result = true;
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed in HandleBankCommand_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        public static bool HandleResetCommand_Prefix(object __instance)
        {
            try
            {
                var archipelago = GetMemberValue<object>(__instance, "_archipelago");
                if (archipelago == null)
                {
                    return false;
                }

                var team = InvokeMethod<object>(archipelago, "GetTeam");
                var key = string.Format(BankingTeamKey, team);
                var scopeEnum = AccessTools.TypeByName("Archipelago.MultiClient.Net.Enums.Scope");
                var globalScope = Enum.Parse(scopeEnum!, "Global");
                InvokeMethod(archipelago, "SetBigIntegerDataStorage", globalScope, key, new BigInteger(0));
                Game1.chatBox?.addMessage(
                    ModEntry.Translation.Get("bank.reset_success", new { amount = 0 }).ToString(),
                    Color.Gold
                );
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed in HandleResetCommand_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        public static bool HandleDepositCommand_Prefix(object __instance, string amount)
        {
            try
            {
                if (!int.TryParse(amount, out var amountToDeposit))
                {
                    InvokeStaticMethod(__instance.GetType(), "PrintUsageRules");
                    return false;
                }

                if (amountToDeposit < MinOperationAmount || amountToDeposit > MaxOperationAmount)
                {
                    Game1.chatBox?.addMessage(
                        ModEntry
                            .Translation.Get(
                                "bank.deposit_range",
                                new { min = MinOperationAmount, max = MaxOperationAmount }
                            )
                            .ToString(),
                        Color.Gold
                    );
                    return false;
                }

                if (amountToDeposit > Game1.player.Money)
                {
                    Game1.chatBox?.addMessage(
                        ModEntry.Translation.Get("bank.deposit_not_enough_money").ToString(),
                        Color.Gold
                    );
                    return false;
                }

                var tax = (int)Math.Round(amountToDeposit * BankTax);
                var realDepositAmount = amountToDeposit - tax;
                var success = InvokeMethod<bool>(__instance, "AddToBank", realDepositAmount);

                if (success)
                {
                    Game1.player.Money -= amountToDeposit;
                    Game1.chatBox?.addMessage(
                        ModEntry.Translation.Get("bank.deposit_success", new { amount = realDepositAmount }).ToString(),
                        Color.Gold
                    );
                    Game1.chatBox?.addMessage(
                        ModEntry.Translation.Get("bank.deposit_tax", new { tax }).ToString(),
                        Color.Gold
                    );
                    Game1.chatBox?.addMessage(ModEntry.Translation.Get("bank.thank_you").ToString(), Color.Gold);
                }
                else
                {
                    Game1.chatBox?.addMessage(ModEntry.Translation.Get("bank.services_down").ToString(), Color.Red);
                }

                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed in HandleDepositCommand_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        public static bool HandleWithdrawCommand_Prefix(object __instance, string amount)
        {
            try
            {
                if (!int.TryParse(amount, out var amountToWithdraw))
                {
                    InvokeStaticMethod(__instance.GetType(), "PrintUsageRules");
                    return false;
                }

                if (amountToWithdraw < MinOperationAmount || amountToWithdraw > MaxOperationAmount)
                {
                    Game1.chatBox?.addMessage(
                        ModEntry
                            .Translation.Get(
                                "bank.withdraw_range",
                                new { min = MinOperationAmount, max = MaxOperationAmount }
                            )
                            .ToString(),
                        Color.Gold
                    );
                    return false;
                }

                var currentBalance = InvokeMethod<BigInteger>(__instance, "GetBankMoneyAmount");
                if (amountToWithdraw > currentBalance)
                {
                    Game1.chatBox?.addMessage(
                        ModEntry.Translation.Get("bank.withdraw_not_enough_money").ToString(),
                        Color.Gold
                    );
                    InvokeMethod(__instance, "PrintCurrentBalance", currentBalance);
                    return false;
                }

                var success = InvokeMethod<bool>(
                    __instance,
                    "RemoveFromBank",
                    new BigInteger(amountToWithdraw)
                );
                if (success)
                {
                    Game1.player.addUnearnedMoney(amountToWithdraw);
                    Game1.chatBox?.addMessage(
                        ModEntry.Translation.Get("bank.withdraw_success", new { amount = amountToWithdraw }).ToString(),
                        Color.Gold
                    );
                    InvokeMethod(__instance, "PrintCurrentBalance");
                    Game1.chatBox?.addMessage(ModEntry.Translation.Get("bank.thank_you").ToString(), Color.Gold);
                }
                else
                {
                    Game1.chatBox?.addMessage(ModEntry.Translation.Get("bank.services_down").ToString(), Color.Red);
                }

                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed in HandleWithdrawCommand_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        public static bool PrintCurrentBalance_Prefix(BigInteger currentBankAmount)
        {
            Game1.chatBox?.addMessage(
                ModEntry.Translation.Get("bank.current_balance", new { amount = currentBankAmount }).ToString(),
                Color.Gold
            );
            return false;
        }

        public static bool PrintUsageRules_Prefix()
        {
            Game1.chatBox?.addMessage(ModEntry.Translation.Get("bank.usage").ToString(), Color.Gold);
            return false;
        }

        private static bool GetSlotDataBool(object archipelago, string memberName)
        {
            var slotData = GetMemberValue(archipelago, "SlotData");
            return slotData != null && GetMemberValue<bool>(slotData, memberName);
        }

        private static T GetMemberValue<T>(object target, string name)
        {
            var value = GetMemberValue(target, name);
            return value is T typed ? typed : default!;
        }

        private static object? GetMemberValue(object? target, string name)
        {
            if (target == null)
            {
                return null;
            }

            var targetType = target as Type ?? target.GetType();
            var property = AccessTools.Property(targetType, name);
            if (property != null)
            {
                return property.GetValue(target is Type ? null : target);
            }

            var field = AccessTools.Field(targetType, name);
            if (field != null)
            {
                return field.GetValue(target is Type ? null : target);
            }

            return null;
        }

        private static T InvokeMethod<T>(object target, string name, params object[] args)
        {
            var result = InvokeMethod(target, name, args);
            return result is T typed ? typed : default!;
        }

        private static object? InvokeMethod(object target, string name, params object[] args)
        {
            var method = AccessTools.Method(target.GetType(), name, GetArgumentTypes(args));
            return method?.Invoke(target, args);
        }

        private static object? InvokeStaticMethod(Type targetType, string name, params object[] args)
        {
            var method = AccessTools.Method(targetType, name, GetArgumentTypes(args));
            return method?.Invoke(null, args);
        }

        private static Type[] GetArgumentTypes(object[] args)
        {
            var types = new Type[args.Length];
            for (var index = 0; index < args.Length; index++)
            {
                types[index] = args[index]?.GetType() ?? typeof(object);
            }

            return types;
        }
    }
}
