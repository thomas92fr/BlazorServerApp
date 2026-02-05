namespace BlazorServerApp.ViewModel.Commons.Fields
{
    /// <summary>
    /// Defines the visual style for command buttons.
    /// Maps to Bootstrap button classes.
    /// </summary>
    public enum CommandStyle
    {
        /// <summary>
        /// Default gray style (btn-secondary).
        /// Use for general actions without specific semantic meaning.
        /// </summary>
        Default,

        /// <summary>
        /// Primary blue style (btn-primary).
        /// Use for main actions like Save, Submit, or primary operations.
        /// </summary>
        Primary,

        /// <summary>
        /// Success green style (btn-success).
        /// Use for creation or positive actions like Add, Create, New.
        /// </summary>
        Success,

        /// <summary>
        /// Danger red style (btn-danger).
        /// Use for destructive actions like Delete, Remove.
        /// </summary>
        Danger,

        /// <summary>
        /// Warning orange/yellow style (btn-warning).
        /// Use for cautionary actions like Discard, Cancel, Reset.
        /// </summary>
        Warning,

        /// <summary>
        /// Info light blue style (btn-info).
        /// Use for informational actions like View, Details, Info.
        /// </summary>
        Info,

        /// <summary>
        /// Light style (btn-light).
        /// Use for subtle actions on dark backgrounds.
        /// </summary>
        Light,

        /// <summary>
        /// Dark style (btn-dark).
        /// Use for prominent actions on light backgrounds.
        /// </summary>
        Dark
    }
}
