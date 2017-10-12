#!/usr/bin/env python

'''
That script is intended to convert a BBFrame (for example Generic Stream TBS output) file to a CADU file.
'''

import sys

ccsdsSync = "\x1A\xCF\xFC\x1D"
frameSize = 2048

def IsSync(data):
  return data[:4] == ccsdsSync

if len(sys.argv) < 3:
  print("Usage: bb2cadu input.ts output.cadu")
  exit(1)

f = open(sys.argv[1], "rb")
o = open(sys.argv[2], "wb")
data = ""

count = 0

print "Reading %s" %sys.argv[1]

while True:
  rd = f.read(32)
  if len(rd) == 0:
    sys.stdout.write("\n")
    break
  data += rd

  while len(data) > 4:
    if IsSync(data):
      data += f.read(2048 - len(data))
      o.write(data)
      count += 1
      data = f.read(4)
      sys.stdout.write("%s frames written\r" % count)
    else:
      data = data[1:]

print "Finished"