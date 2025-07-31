Welcome to Streamer Social Media Manager(SSMM) by cybercola/GodWasaProgrammer

How To use this application:

Requirements:
Docker Desktop(installation required)
Nginx-rtmp-server(no installation required)


1.
After you have successfully installed docker desktop, run this command in the "terminal" window of docker desktop.

docker run --rm -it -p 1935:1935 -p 8080:80 tiangolo/nginx-rtmp

After doing so, look at your SSSMM window, you should see the indicator for RTMP server showing a ✅ Online indicator for RTMP Server.

2.
Select which services you wish to stream to, and add the relevant streamkey when prompted.

3.
Go to OBS(or other RTMP-viable streaming software)
Go into Settings -> Stream -> "Custom" set server to : rtmp://localhost/live/stream

4. 
Click "Start Receiving in SSMM"
Your stream should now be visible in the top left corner, make sure all is looking good.

5.
Click in SSMM -> Start Stream

6.
Enjoy Free multistreaming to as many platforms as you wish.