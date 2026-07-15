# Inventory UGUI composition

Instantiate `InventoryShell.prefab` or compose the smaller role prefabs yourself.
The presenter owns aggregate commands; view components receive immutable models and
emit selection/activation intent. Replace a slot or item nested instance with a
prefab variant without unpacking the shell.

`InventoryStateGallery` can replay every required presentation state without a
product database or XR package. World-space Canvas and tracked-device interaction
belong to the optional XR adapter.
