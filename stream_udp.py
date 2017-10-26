#!/usr/bin/env python

import socket, time


sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) # UDP

#file = "/media/ELTN/Baseband Records/GOES/GRB/TBSCapture/Q-capture1.ts"
#file = "/home/lucas/Works/OpenSatelliteProject/split/grbdump/Q-capture.cadu"
file = "/media/ELTN/Baseband Records/GOES/GRB/TestData/cspp-geo-grb-test-data-0.4.6/CADU_5"
#file = "/media/ELTN/Baseband Records/GOES/GRB/TestData/cspp-geo-grb-test-data-0.4.6/CADU_6"
#file = "/media/lucas/0E003F18003F05ED1/CADU/CADU_6"
SearchBuffSize = 2048
MaxBuffSize = 16384
ccsdsSync = "\x1A\xCF\xFC\x1D"

TCP_IP = '127.0.0.1'
TCP_PORT = 5001
FAST_MODE = True # Assume all CADUs are correct, starting with the start of file.


def searchInBuff(buff, token):
  buff = bytearray(buff)
  token = bytearray(token)
  if len(buff) > len(token):
    for i in range(len(buff) - len(token)):
      found = True
      for z in range(len(token)):
        if buff[i+z] != token[z]:
          found = False
          break
      if found:
        return i
  return None

def IsSync(data):
  return data[:4] == '\x1A\xCF\xFC\x1D'

f = open(file, "rb")

data = ""
frameCount = 0

time.sleep(1)

if FAST_MODE:
  # Assume Sequential CADUs
  while True:
    rd = f.read(2048)
    if len(rd) < 2048:
      break
    frameCount+=1
    sock.sendto(rd, ('127.0.0.1', 1234))
    time.sleep(0.001) # Full Speed
    #time.sleep( 0.002)
    # time.sleep(0.01)
else:
  while True:
    rd = f.read(SearchBuffSize)
    if len(rd) == 0:
      break
    data += rd
    if not IsSync(data):
      print "Searching for Sync Mark"
      pos = searchInBuff(data, ccsdsSync)
      if pos != None:
        print "Found sync at: %s" %pos
        print "Buff Len: %s" %len(data)
        data = data[pos:]
      else:
        print "Sync not found, trying one more cycle"
        data = data[len(data)-MaxBuffSize:]
        continue

    remaining = ""

    if len(data) < 2048:
      data += f.read(2048 - len(data))
    elif len(data) > 2048:
      remaining += data[2048:]
      data = data[:2048]
    sock.sendto(data, ('127.0.0.1', 1234))
    frameCount += 1
    data = remaining
    time.sleep( 0.001)

conn.close()
f.close()