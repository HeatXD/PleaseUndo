extends Control

# Called when the node enters the scene tree for the first time.
func _ready():
	pass # Replace with function body.


func _on_BTN_START_button_up():
	var loc_port:int = $BG/LE_LOCALPORT.text.to_int()
	var rem_port:int = $BG/LE_REMOTEPORT.text.to_int()
	var rem_ip:String = $BG/LE_REMOTEIP.text.strip_escapes()
	var loc_id:int = $BG/LE_LOCALID.text.to_int()
	var loc_delay:int = $BG/LE_DELAY.text.to_int()
	
	if(loc_port != 0 && loc_id != 0 && !rem_ip.empty() && rem_port != 0):
		NetworkGlobals.player_id = loc_id
		NetworkGlobals.local_port = loc_port
		NetworkGlobals.remote_addr = rem_ip
		NetworkGlobals.remote_port = rem_port
		NetworkGlobals.local_delay = loc_delay
		#switch to the gameplay scene
		get_tree().change_scene("res://scenes/Gameplay.tscn")
	else:
		OS.alert("Please fill in all the required information!")

func _on_BTN_1P_pressed():
	$BG/LE_LOCALPORT.text = "7001"
	$BG/LE_REMOTEPORT.text = "7002"
	$BG/LE_REMOTEIP.text = "127.0.0.1"
	$BG/LE_LOCALID.text = "1"
	$BG/LE_DELAY.text = "0"

func _on_BTN_2P_pressed():
	$BG/LE_LOCALPORT.text = "7002"
	$BG/LE_REMOTEPORT.text = "7001"
	$BG/LE_REMOTEIP.text = "127.0.0.1"
	$BG/LE_LOCALID.text = "2"
	$BG/LE_DELAY.text = "0"
