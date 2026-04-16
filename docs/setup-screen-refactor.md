# Setup Screen Refactor

The setup screen should consist of these pages:

1. Prompt for language (default: follow system), light/dark theme (default: follow system). Each should be a line with icon and text in the front (language: globe, theme: palette). Title: “Welcome to PCL-ME".
2. Alert the user that this version is a community-built version, and they should acknowledge this and agree to the eula before proceeding. Title: "Accepting EULA".
3. Telling the user all is set, adding tutorial and links on the screen.

The setup screen should be a centered modal card that blocks all interaction with the app, including the top bar. Everything should be i18n driven, and choosing a different language should immediately update the text on the screen.