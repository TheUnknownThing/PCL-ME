# UI Observations

## Title Bar

Defined primarily in `PCL.Frontend.Spike/Desktop/MainWindow.axaml`

[x] When toggling modes, the white backdrop should fade in instead of popping in without animation.<br>
[ ] Remove the "Maximize" button from the title bar.<br>
[x] In context mode (indicating the window displays a specific information), the PCL CE logos and all other LFS components should be hidden, leaving only the text for the context. See image:<br>
![Context](img/instance-selection.png)
[ ] In context mode, pressing `ESC` cannot return to the page before the current context.<br>

## Instance Selection

Have not been implemented yet, see image for reference:<br>
![Instance Selection](img/instance-selection.png)
![Instance Selection (Instance Available)](img/instance-selection-available.png)

[ ] Empty minecraft dir should provide a dialog telling user about the current state, see image 1.<br>
[ ] Filled minecraft dir should provide a list of instances, see image 2.<br>
