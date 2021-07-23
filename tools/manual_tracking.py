# coding: utf-8
import cv2
import numpy as np
import sys
from math import *

# 起始帧
id = 1

fov = 30
px = 0
py = 0

def on_EVENT_LBUTTONDOWN(event, x, y, flags, param):
    if event == cv2.EVENT_LBUTTONDOWN:
        global px,py
        px = x
        py = y
        loadImg()

flag = {}

def bfs(x0, y0):
    if x0 < 0 or x0 >= 1024 or y0 < 0 or y0 >= 512:
        return
    if str(x0) + "," + str(y0) in flag:
        return

    x = (px - 512) * pi / 512
    y = (py - 256) * pi / 512
    x1 = (x0 - 512) * pi / 512
    y1 = (y0 - 256) * pi / 512
    theta = acos(cos(x-x1) * cos(y) * cos(y1) + sin(y) * sin(y1))
    if round(theta / pi * 180) == fov:
        cv2.circle(img, (x0, y0), 1, (0, 0, 255), thickness=-1)
        flag[str(x0) + "," + str(y0)] = True
        bfs(x0 - 1, y0)
        bfs(x0 + 1, y0)
        bfs(x0, y0 - 1)
        bfs(x0, y0 + 1)

def loadImg():
    global img
    # 在这里修改图片文件命名格式
    img = cv2.imread("res" + str(id) + "_L.png")
    cv2.putText(img, str(id), (0, 15), cv2.FONT_HERSHEY_PLAIN,
                    1.0, (255, 255, 255), 2, cv2.LINE_AA)
    cv2.putText(img, str(id), (0, 15), cv2.FONT_HERSHEY_PLAIN,
                    1.0, (0, 0, 0), 1, cv2.LINE_AA)
    cv2.putText(img, "fov=%d" % fov, (0, 30), cv2.FONT_HERSHEY_PLAIN,
                    1.0, (255, 255, 255), 2, cv2.LINE_AA)
    cv2.putText(img, "fov=%d" % fov, (0, 30), cv2.FONT_HERSHEY_PLAIN,
                    1.0, (0, 0, 0), 1, cv2.LINE_AA)
    if px != 0 or py != 0:
        cv2.circle(img, (px, py), 1, (0, 0, 255), thickness=-1)
        cv2.putText(img, "%d,%d" % (px, py), (px + 5, py - 5), cv2.FONT_HERSHEY_PLAIN,
                    1.0, (255, 255, 255), 2, cv2.LINE_AA)
        cv2.putText(img, "%d,%d" % (px, py), (px + 5, py - 5), cv2.FONT_HERSHEY_PLAIN,
                    1.0, (0, 0, 0), 1, cv2.LINE_AA)
        # cv2.circle(img, (px, py), int(fov * 512 / 360), (0, 0, 255), thickness=0)

        global flag
        flag = {}
        if py - round(fov * 512 / 180) >= 0:
            bfs(px, py - round(fov * 512 / 180))
        elif py + round(fov * 512 / 180) < 512:
            bfs(px, py + round(fov * 512 / 180)) 
            
    
    cv2.imshow("image", img)
    
sys.setrecursionlimit(1000000)
cv2.namedWindow("image")
cv2.setMouseCallback("image", on_EVENT_LBUTTONDOWN)
loadImg()

f = open("data.txt", "a+")

while (True):
    k = cv2.waitKey(0)
    if k == ord('d'):
        id += 1
    elif k == ord('a'):
        if id != 1:
            id -= 1
    elif k == ord('r'):
        px = 0
        py = 0
    elif k == ord('w'):
        fov += 1
    elif k == ord('s'):
        fov -= 1
    elif k == ord('e'):
        f.write("%d %d %d %d\n" % (id, px, py, fov))
        f.flush()
    elif k == ord('q'):
        f.close()
        break
    loadImg()

cv2.destroyAllWindow()
sys.exit()