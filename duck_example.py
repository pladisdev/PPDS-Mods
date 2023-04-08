#pip install paho-mqtt
import paho.mqtt.client as mqtt

import json
import time
import random

class Duck_Example:

    def __init__(self, broker_address, port):

        self.subcribe_list = ["ppds/duckid"]

        # Connect to MQTT broker
        try:
            self.client = mqtt.Client()
            self.client.on_disconnect = self.on_disconnect
            self.client.on_connect = self.on_connect
            self.client.on_message = self.on_message
            self.client.connect(host=broker_address, port=port)
            
            # Start MQTT loop (runs forever)
            self.client.loop_start()
        except Exception as e:
            print("error failed to connect to MQTT broker")
            raise e

        #Longer initial waits so things can initialize
        self.quack_time = time.time() + 10
        self.tab_time = time.time() + 10

        self.duck_naming = False

    # Define callback functions for MQTT events
    def on_connect(self, client, userdata, flags, rc):
        print("Connected with result code " + str(rc))
        # Subscribe to the topic
        for subcribe in self.subcribe_list:
            client.subscribe(subcribe)          

    def on_message(self, client, userdata, msg):
        if msg.topic == "ppds/duckid":
            try:
                duck_stuff = json.loads(msg.payload)

                duck_name = duck_stuff["DuckName"] 
                duck_id = duck_stuff["DuckID"] 
            except:
                print("MQTT message in wrong format")

            #Check if the duck has a name, rename the duck if it doesn't
            if duck_stuff["DuckName"] == "":
                self.duck_naming = True
                print(f"You are looking at a {duck_id} duck with no name. What do you want to name it?")
                client.publish("ppds/duckname", input())
                self.duck_naming = False
                self.tab_time = time.time() + 5
            else:
                print(f"You are looking at a {duck_id} duck with the name '{duck_name}'.")


    def on_disconnect(self, client, userdata, msg):
        print("disconnected")
    
    def __call__(self):   

        while not self.client.is_connected():
            time.sleep(.5)
            print("Waiting for connection with MQTT Broker...")

        print("Stop example with CTRL C. Duck will quack randomly, and selected duck will change every 5 seconds. You can name ducks that don't have names.")
        try: 
            while True:
                
                current_time = time.time()

                #Quack the duck
                if current_time > self.quack_time:
                    self.client.publish("ppds/quack", "1")
                    self.quack_time = time.time() + random.randint(1, 20)

                #Don't change ducks (Pressing TAB) while naming the duck
                if current_time > self.tab_time and not self.duck_naming:
                    self.client.publish("ppds/tab", "1")
                    self.tab_time = time.time() + 5
                
                time.sleep(0.01)
        except:
            pass
            
        print("Ending program and disconnecting from MQTT")
        self.client.loop_stop()
        self.client.disconnect()

def main():
    broker_address = "localhost"
    port = 1883

    try:
        de = Duck_Example(broker_address, port)
    except Exception as e:
        print(e)
    else:
        de()

if(__name__ == '__main__'):
    main()
        
