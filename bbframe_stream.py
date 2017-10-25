#!/usr/bin/env python

import binascii, time, socket

file = "/media/ELTN/Baseband Records/GOES/GRB/TBSCapture/Q-capture1.ts"

FrameLength = 7274
SearchBuffSize = FrameLength * 2
MaxBuffSize = FrameLength * 16

BBFRAME_HEADER = '\x71\x00\x00\x00\xE3'


UDP_IP = '127.0.0.1'
UDP_PORT = 1234


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
  return data[:5] == BBFRAME_HEADER

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) # UDP



f = open(file, "rb")
data = ""
frameCount = 0
while True:
  rd = f.read(SearchBuffSize)
  if len(rd) == 0:
    break
  data += rd
  if not IsSync(data):
    print "Searching for Sync Mark"
    pos = searchInBuff(data, BBFRAME_HEADER)
    if pos != None:
      print "Found sync at: %s" %pos
      print "Buff Len: %s" %len(data)
      data = data[pos:]
    else:
      print "Sync not found, trying one more cycle"
      data = data[len(data)-MaxBuffSize:]
      continue

  remaining = ""

  if len(data) < FrameLength:
    data += f.read(FrameLength - len(data))
  elif len(data) > FrameLength:
    remaining += data[FrameLength:]
    data = data[:FrameLength]
  sock.sendto(data, (UDP_IP, UDP_PORT))
  #print binascii.hexlify(data[:10]).upper()
  frameCount += 1
  data = remaining
  time.sleep(0.0038)

conn.close()
f.close()