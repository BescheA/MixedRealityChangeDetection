import json
import os
import numpy as np
from scipy.spatial.transform import Rotation as R
import argparse
import codecs

parser = argparse.ArgumentParser()

parser.add_argument("--input_dir")
parser.add_argument("--output_dir")
parser.add_argument("--transform",action='store_true')

args = parser.parse_args()

if args.transform:
    trans = np.eye(4)
    rotx = R.from_rotvec(-np.pi/2*np.array([1,0,0]))
    trans[:3,:3] = rotx.as_matrix()

    p = np.eye(4)
    p[1,1] = -1

os.makedirs(args.output_dir, exist_ok=True)

res = []
res.append(["ImageID","TimeStamp"])
for name in os.listdir(args.input_dir):
    if name.endswith(".json") and "pose" in name and os.path.isfile(os.path.join(args.input_dir, name)):
        file_path = os.path.join(args.input_dir, name)

        index = name.split("_")[0]
        output_path = os.path.join(args.output_dir, f"{index}_pose.txt")

        try:
            with open(file_path, "r") as file:
                data = json.load(file)
        except:
            data = json.load(codecs.open(file_path, 'r', 'utf-8-sig'))


        timestamp = str(data['timestamp_ns'])

        res.append([index,timestamp])

        try:
            extrinsics = data["pose"]["extrinsics"]
        except:
            print(name)

        matrix = np.array([
            [extrinsics["e00"], extrinsics["e01"], extrinsics["e02"], extrinsics["e03"]],
            [extrinsics["e10"], extrinsics["e11"], extrinsics["e12"], extrinsics["e13"]],
            [extrinsics["e20"], extrinsics["e21"], extrinsics["e22"], extrinsics["e23"]],
            [extrinsics["e30"], extrinsics["e31"], extrinsics["e32"], extrinsics["e33"]],
        ])

        if args.transform:
            matrix = p @ matrix @ p
            matrix = trans @ matrix

        np.savetxt(output_path, matrix, fmt="%.6f", delimiter=" ")

        #print(f"Saved at {output_path}")

res = np.asarray(res)
np.savetxt(os.path.join(args.output_dir,"timestamps.csv"),res,delimiter=',',fmt='%s')