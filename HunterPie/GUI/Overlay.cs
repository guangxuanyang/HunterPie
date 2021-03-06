﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using HunterPie.Core;
using HunterPie.Logger;
using HunterPie.Memory;

namespace HunterPie.GUI
{
    public class Overlay : IDisposable
    {
        public bool IsDisposed { get; private set; }

        private static readonly List<Widget> widgets = new List<Widget>();

        public static IReadOnlyList<Widget> Widgets => widgets;

        Game Context { get; set; }

        public Overlay(Game ctx)
        {
            Context = ctx;
            SetRenderMode();
            CreateWidgets();
        }

        #region Overlay static methods

        /// <summary>
        /// Registers a new widget to the overlay
        /// </summary>
        /// <param name="widget">Widget to register</param>
        /// <returns>True if the widget was registered succesfully.</returns>
        public static bool RegisterWidget(Widget widget)
        {
            try
            {
                widgets.Add(widget);
                return true;
            } catch (Exception err)
            {
                Debugger.Error(err);
                return false;
            }
        }

        /// <summary>
        /// Unregisters a widget from the overlay
        /// </summary>
        /// <param name="widget">Widget to unregister</param>
        /// <returns>True if the widget was unregistered succesfully.</returns>
        public static bool UnregisterWidget(Widget widget)
        {
            try
            {
                widgets.Remove(widget);
                return true;
            } catch (Exception err)
            {
                Debugger.Error(err);
                return false;
            }
        }

        #endregion

        internal void ToggleDesignMode()
        {
            foreach (Widget widget in Widgets)
            {
                widget.InDesignMode = !widget.InDesignMode;
            }
            if (!Widgets.First().InDesignMode) UserSettings.SaveNewConfig();
        }

        private void SetRenderMode()
        {
            if (!UserSettings.PlayerConfig.Overlay.EnableHardwareAcceleration)
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }
        }

        private void CreateWidgets()
        {
            RegisterWidget(new Widgets.HarvestBox(Context.Player));
            RegisterWidget(new Widgets.MantleTimer(0, Context.Player.PrimaryMantle));
            RegisterWidget(new Widgets.MantleTimer(1, Context.Player.SecondaryMantle));
            RegisterWidget(new Widgets.MonsterContainer(Context));
            RegisterWidget(new Widgets.DPSMeter.Meter(Context));
            for (int AbnormTrayIndex = 0; AbnormTrayIndex < UserSettings.PlayerConfig.Overlay.AbnormalitiesWidget.ActiveBars; AbnormTrayIndex++)
            {
                RegisterWidget(new Widgets.Abnormality_Widget.AbnormalityContainer(Context.Player, AbnormTrayIndex));
            }
            RegisterWidget(new Widgets.ClassWidget.ClassWidgetContainer(Context));
            RegisterWidget(new Widgets.HealthWidget.PlayerHealth(Context));
        }

        private void DestroyWidgets()
        {
            if (!Widgets.First().InDesignMode) UserSettings.SaveNewConfig();
            foreach (Widget widget in Widgets)
            {
                widget.InDesignMode = false;
                widget.Close();
            }
            widgets.Clear();
        }

        internal void HookEvents()
        {
            Kernel.OnGameFocus += OnGameFocus;
            Kernel.OnGameUnfocus += OnGameUnfocus;
        }

        private void UnhookEvents()
        {
            Kernel.OnGameFocus -= OnGameFocus;
            Kernel.OnGameUnfocus -= OnGameUnfocus;
        }

        public void Destroy()
        {
            DestroyWidgets();
            UnhookEvents();
            Context = null;
        }

        public void GlobalSettingsEventHandler(object source, EventArgs e)
        {
            ToggleOverlay();
            DeleteAbnormWidgetsIfNeeded();
            foreach (Widget widget in Widgets)
            {
                widget.ApplySettings();
            }
        }

        private void DeleteAbnormWidgetsIfNeeded()
        {
            List<int> IndexesToRemove = new List<int>();
            int i = 0;
            foreach (Widget widget in Widgets)
            {
                if (widget.Type == WidgetType.AbnormalityWidget)
                {
                    Widgets.Abnormality_Widget.AbnormalityContainer widgetConverted = (Widgets.Abnormality_Widget.AbnormalityContainer)widget;
                    if (widgetConverted.AbnormalityTrayIndex >= UserSettings.PlayerConfig.Overlay.AbnormalitiesWidget.ActiveBars)
                    {
                        widgetConverted.Close();
                        IndexesToRemove.Add(i);
                    }
                }
                i++;
            }
            foreach (int index in IndexesToRemove)
            {
                widgets.RemoveAt(index);
            }

        }

        private async void OnGameUnfocus(object source, EventArgs args)
        {
            await Application.Current.Dispatcher.InvokeAsync(new Action(() =>
            {
                foreach (Widget widget in Widgets)
                {
                    widget.OverlayIsFocused = false;
                    if (!widget.InDesignMode)
                    {
                        widget.ApplySettings(true);
                    }
                }
            }));
        }

        private async void OnGameFocus(object source, EventArgs args)
        {
            await Application.Current.Dispatcher.InvokeAsync(new Action(() =>
            {
                foreach (Widget widget in Widgets)
                {
                    widget.OverlayIsFocused = true;
                    if (!widget.InDesignMode)
                    {
                        widget.ApplySettings(true);
                    }
                }
            }));
        }

        /* Positions and enable/disable components */

        public void ToggleOverlay()
        {
            foreach (Widget widget in Widgets)
            {
                widget.OverlayActive = UserSettings.PlayerConfig.Overlay.Enabled;
                widget.OverlayFocusActive = UserSettings.PlayerConfig.Overlay.HideWhenGameIsUnfocused;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnhookEvents();
                Destroy();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
