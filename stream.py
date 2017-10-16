#!/usr/bin/env python

import socket, time

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

def parseFrame(data, conn):
  #if data[:4] == '\x1A\xCF\xFC\x1D':
  #  print "OK"
  data = data[4:]
  scid = ((ord(data[0]) & 0x3F) << 2) + ( (ord(data[1]) & 0xC0) >> 6)
  vcid = ((ord(data[1]) & 0x3F))
  counter = ord(data[4]) + (ord(data[3]) << 8) + (ord(data[2]) << 16)

  if vcid != 63:
    #print "Valid Frame"
    #print "   Satellite ID: %s" %scid
    #if vcid == 5:
    #  print "   RHCP Channel"
    #elif vcid == 6:
    #  print "   LHCP Channel"
    #else:
    #  print "   Virtual Channel: %s" %vcid
    print "   Counter: %s" %counter
    #print ""
    conn.send(data[:2042])

f = open(file, "rb")

data = ""
frameCount = 0

s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
s.bind((TCP_IP, TCP_PORT))
s.listen(1)

print "Waiting connection"
conn, addr = s.accept()
print 'Connection address:', addr
#conn = None
time.sleep(1)

if FAST_MODE:
  # Assume Sequential CADUs
  while True:
    rd = f.read(2048)
    if len(rd) < 2048:
      break
    frameCount+=1
    parseFrame(rd, conn)
    #time.sleep(0.00015) # Full Speed
    time.sleep( 0.002)
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
    parseFrame(data, conn)
    frameCount += 1
    data = remaining
    time.sleep( 0.002)

conn.close()
f.close()