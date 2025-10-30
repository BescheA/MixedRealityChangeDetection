import json
import os
import numpy as np

directory = "/Users/konch/Downloads/session_20251027_134036"
output_directory = os.path.join(directory, "matrices_txt")

os.makedirs(output_directory, exist_ok=True)

for name in os.listdir(directory):
    if name.endswith(".json") and os.path.isfile(os.path.join(directory, name)):
        file_path = os.path.join(directory, name)

        index = name[6:].replace(".json", "")
        output_path = os.path.join(output_directory, f"{index}_pose.txt")

        with open(file_path, "r") as file:
            data = json.load(file)
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

            np.savetxt(output_path, matrix, fmt="%.6f", delimiter=" ")

        print(f"Saved {output_path}")
