#!/usr/bin/env python

import sys, glymur, numpy as np, time
from PIL import Image

if len(sys.argv) < 2:
  print "Usage: jp2png output.png image1.jp2 [image2.jpg] ..."
  exit(1)

fulldata = None
output = sys.argv[1]
files = sys.argv[2:]
lines = []
files.sort()

lastnum = -1
c = 1

blockshape = (128, 904)
mincalc = None
segs = 1
for file in files:
  print "Reading %s" %file
  try:
    data = glymur.Jp2k(file)
    if fulldata == None:
      fulldata = data[:]
      mincalc = data[:]
      blockshape = fulldata.shape
    else:
      try:
        fulldata = np.append(fulldata, data[:], axis=0)
        mincalc = np.append(mincalc, data[:], axis=0)
      except e:
        break
  except:
    fulldata = np.append(fulldata, np.zeros(blockshape), axis=0)
'''
for file in files:
  print "Reading %s" %file
  num = int(file.split("-")[1].split(".")[0])
  data = glymur.Jp2k(file)
  if fulldata == None:
    fulldata = data[:]
    blockshape = fulldata.shape
    lastnum = num
  else:
    while lastnum + 1 != num:
      print "Skipped %s" % (lastnum + 1)
      if c == segs:
        lines.append(fulldata)
        fulldata = np.zeros(blockshape)
      else:
        fulldata = np.append(fulldata, np.zeros(blockshape), axis=1)
      c = c + 1
      lastnum += 1
    else:
      if c == segs:
        c = 0
        lines.append(fulldata)
        fulldata = data[:]
      else:
        try:
          fulldata = np.append(fulldata, data[:], axis=1)
        except:
          break
      c = c + 1
      lastnum = num

#lines.append(fulldata)
fulldata = None

for i in lines:
  if fulldata == None:
    fulldata = i
  else:
    print i.shape, fulldata.shape
    try:
      fulldata = np.append(fulldata, i, axis=0)
    except:
      break
'''
print "Processing"
print fulldata.max()
if fulldata.max() > 255:
  baseline = mincalc.min()
  print "Baseline: %s" %baseline
  z = fulldata - baseline
  mincalc = mincalc - baseline
  z = np.clip(z, 0, z.max())
  divz = mincalc.max() / 255
  print divz
  z = z / divz
else:
  print "No Baseline correction"
Image.fromarray(z.astype(dtype="uint8")).save("%s" %output)