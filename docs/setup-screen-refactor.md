# Setup Screen Refactor

The setup screen should consist of these pages:

1. Prompt for language (default: follow system), light/dark theme (default: follow system). Each should be a line with icon and text in the front (language: globe, theme: palette). Title: “Welcome to PCL-ME".
2. Alert the user that this version is a community-built version, and they should acknowledge this and agree to the eula before proceeding. Title: "Terms of Use".
3. Telling the user all is set, adding tutorial and links on the screen.

The setup screen should be a centered modal card that blocks all interaction with the app, including the top bar. Everything should be i18n driven, and choosing a different language should immediately update the text on the screen.

您正在使用 PCL-ME。这是基于 PCL 的社区跨平台版本，源码在 Github 上完全开放。在使用该软件的时候，请遵守你所在国家/地区的法律法规，尊重 PCL 原作者的[用户协议与免责声明](https://shimo.im/docs/rGrd8pY8xWkt6ryW)。如果你想要开发、分发基于 PCL-ME 的软件，请先阅读我们的[README](https://github.com/theUnknownThing/PCL-ME)中关于许可证的说明。
