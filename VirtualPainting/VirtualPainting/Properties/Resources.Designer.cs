﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace VirtualPainting.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("VirtualPainting.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Construct.
        /// </summary>
        internal static string PaintingHeader {
            get {
                return ResourceManager.GetString("PaintingHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to a new identity with paint.
        /// </summary>
        internal static string PaintingSubHeader {
            get {
                return ResourceManager.GetString("PaintingSubHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap SavedImageFrame {
            get {
                object obj = ResourceManager.GetObject("SavedImageFrame", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Saved.
        /// </summary>
        internal static string SavingImageHeader {
            get {
                return ResourceManager.GetString("SavingImageHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to to the iPad for future reference.
        /// </summary>
        internal static string SavingImageSubHeader {
            get {
                return ResourceManager.GetString("SavingImageSubHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Snapshot!.
        /// </summary>
        internal static string SnapshotHeader {
            get {
                return ResourceManager.GetString("SnapshotHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Smile!.
        /// </summary>
        internal static string WaitingForPresenceHeader {
            get {
                return ResourceManager.GetString("WaitingForPresenceHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to to capture a base layer image.
        /// </summary>
        internal static string WaitingForPresenceSubHeader {
            get {
                return ResourceManager.GetString("WaitingForPresenceSubHeader", resourceCulture);
            }
        }
    }
}