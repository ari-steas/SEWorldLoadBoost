﻿using HarmonyLib;
using Sandbox.Engine.Utils;
using Sandbox;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.Game.Gui;
using Sandbox.Game.Screens;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using ClientPlugin;
using ParallelTasks;
using Sandbox.Game.Multiplayer;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity.UseObject;
using VRage.Game;
using VRage.ObjectBuilders.Private;
using VRage.ObjectBuilders;
using VRage.Plugins;
using VRage.Utils;
using Sandbox.Game.GUI;

[HarmonyPatch(typeof(MyScriptManager), "LoadData")]
public class MyScriptManager_LoadDataPatch
{
    public static bool IsLoading { get; private set; } = false;
    public static List<(MyModContext, MyStringId, Assembly)> AssembliesToAdd;

    [HarmonyPrefix]
    public static bool Prefix(MyScriptManager __instance)
    {
        MySandboxGame.Log.WriteLine("PATCHED MyScriptManager.LoadData() - START");
		MySandboxGame.Log.IncreaseIndent();

        AssembliesToAdd = new List<(MyModContext, MyStringId, Assembly)>();
        IsLoading = true;

		MyScriptManager.Static = __instance;
		__instance.Scripts.Clear();
		__instance.EntityScripts.Clear();
		__instance.SubEntityScripts.Clear();
		__instance.Call("TryAddEntityScripts", MyModContext.BaseGame, MyPlugins.SandboxAssembly);
        __instance.Call("TryAddEntityScripts", MyModContext.BaseGame, MyPlugins.SandboxGameAssembly);
		if (MySession.Static.CurrentPath != null)
		{
            __instance.Call("LoadScripts", MySession.Static.CurrentPath, MyModContext.BaseGame);
		}
		if (MySession.Static.Mods != null)
		{
			MyGuiScreenLoading firstScreenOfType = MyScreenManager.GetFirstScreenOfType<MyGuiScreenLoading>();
            firstScreenOfType?.SetLocalTotal((float)MySession.Static.Mods.Count);
            bool isServer = Sync.IsServer;

            Parallel.ForEach(MySession.Static.Mods, modItem => LoadScript_Multithread(modItem, firstScreenOfType, isServer, __instance), WorkPriority.High, blocking: false);
        }
		foreach (Assembly assembly in __instance.Scripts.Values)
		{
			if (MyFakes.ENABLE_TYPES_FROM_MODS)
			{
				MyObjectBuilderType.RegisterFromAssembly(assembly, false);
				MyComponentFactory.Static.RegisterFromAssembly(assembly);
				MyComponentTypeFactory.Static.RegisterFromAssembly(assembly);
				MyObjectBuilderSerializerKeen.RegisterFromAssembly(assembly);
			}
			MySandboxGame.Log.WriteLine($"Script loaded: {assembly.FullName}");
		}
		MyTextSurfaceScriptFactory.LoadScripts();
		MyUseObjectFactory.RegisterAssemblyTypes(__instance.Scripts.Values.ToArray<Assembly>());

        IsLoading = false;

        // Sidestep threading problems
        foreach (var assemblyTuple in AssembliesToAdd)
        {
            __instance.Call("AddAssembly", assemblyTuple.Item1, assemblyTuple.Item2, assemblyTuple.Item3);
            MySandboxGame.Log.WriteLine("AddAssembly " + assemblyTuple.Item2.String);
        }
        AssembliesToAdd = null;

		MySandboxGame.Log.DecreaseIndent();
		MySandboxGame.Log.WriteLine("MyScriptManager.LoadData() - END");

        // Return false to skip the original method
        return false;
    }

    private static void LoadScript_Multithread(MyObjectBuilder_Checkpoint.ModItem modItem, MyGuiScreenLoading firstScreenOfType, bool isServer, MyScriptManager __instance)
    {
        bool flag = false;
        if (modItem.IsModData())
        {
            ListReader<string> tags = modItem.GetModData().Tags;
            if (tags.Contains(MySteamConstants.TAG_SERVER_SCRIPTS) && !isServer)
            {
                return;
            }
            flag = tags.Contains(MySteamConstants.TAG_NO_SCRIPTS);
        }
        MyModContext myModContext = (MyModContext)modItem.GetModContext();
        try
        {
            __instance.Call("LoadScripts", modItem.GetPath(), myModContext);
            firstScreenOfType?.AddLocalProgress(1f);
            //MySandboxGame.Log.WriteLine($"Parallel loaded: {modItem.FriendlyName}");
        }
        catch (MyLoadingRuntimeCompilationNotSupportedException)
        {
            if (!flag)
            {
                throw;
            }
            MyVRage.Platform.Scripting.ReportIncorrectBehaviour(MyCommonTexts.ModRuleViolation_RuntimeScripts);
        }
        catch (Exception ex)
        {
            MyLog.Default.WriteLine(
                $"Fatal error compiling {myModContext.ModServiceName}:{myModContext.ModId} - {myModContext.ModName}. This item is likely not a mod and should be removed from the mod list.");
            MyLog.Default.WriteLine(ex);
            throw;
        }
    }
}

[HarmonyPatch(typeof(MyScriptManager), "AddAssembly")]
public class MyScriptManager_AddAssemblyPatch
{
    [HarmonyPrefix]
    public static bool Prefix(MyModContext context, MyStringId myStringId, Assembly assembly)
    {
        if (!MyScriptManager_LoadDataPatch.IsLoading)
            return true; // Run the original method
        MyScriptManager_LoadDataPatch.AssembliesToAdd.Add((context, myStringId, assembly));
        return false; // Break early and skip original method to avoid parallelization issues
    }
}