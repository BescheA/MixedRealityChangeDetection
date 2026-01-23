import viser
import numpy as np
from scipy.spatial.transform import Rotation as R
import json
import argparse
import os
import time

def dict2mat(matdict):
    mat = np.zeros((4,4))
    for i in range(4):
        for j in range(4):
            mat[i,j] = matdict[f"e{i}{j}"]

    return mat

def lines2mat(lines):
    mat = np.zeros((4,4))
    for i in range(4):
        parts = lines[i].split()
        for j in range(4):
            mat[i,j] = float(parts[j])
    return mat


def visualize_matrices(matrices, axis_length=0.5, axis_width=0.02):
    """
    Visualize a list of 4x4 transformation matrices using viser.
    
    Args:
        matrices: List of 4x4 numpy arrays
        axis_length: Length of the coordinate frame axes
        axis_width: Width of the coordinate frame axes
    """
    server = viser.ViserServer()

    print(f"Visualizing {len(matrices)} matrices")
    print(f"Open browser at: http://localhost:{server.get_port()}")
    
    # Add a ground plane for reference
    server.scene.add_grid(
        name="/ground",
        width=20,
        height=20,
        width_segments=20,
        height_segments=20,
        cell_color=(200, 200, 200),
        cell_thickness=1.0,
        section_color=(150, 150, 150),
        section_thickness=2.0,
    )


    # Visualize each matrix as a coordinate frame
    for idx, matrix in enumerate(matrices):
        # Extract position and rotation
        position = matrix[:3, 3]
        rotation_matrix = matrix[:3, :3]

        # Convert rotation matrix to quaternion (wxyz format for viser)
        rot = R.from_matrix(rotation_matrix)
        quat_wxyz = rot.as_quat(scalar_first=True)  # scipy gives xyzw

        # Add coordinate frame
        server.scene.add_frame(
            name=f"/frame_{idx}",
            wxyz=quat_wxyz,
            position=position,
            axes_length=axis_length,
            axes_radius=axis_width,
        )

        time.sleep(0.5)

    
    print("\nPress Ctrl+C to exit")
    
    # Keep the server running
    try:
        while True:
            pass
    except KeyboardInterrupt:
        print("\nShutting down...")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()

    parser.add_argument("--input_dir")
    parser.add_argument("--format",default="json")
    parser.add_argument("--limit",type=int,default="-1")
    parser.add_argument("--step",type=int,default="4")


    args = parser.parse_args()

    assert args.format in ['json','txt'], "format should be in ['json','txt']"

    all_files = os.listdir(args.input_dir)
    all_files.sort() #sort over time

    #subsample
    all_files = all_files[::args.step]

    if args.limit > 0:
        all_files = all_files[:args.limit]
    
    #trans to apply
    rotx = R.from_rotvec(-np.pi/2*np.array([1,0,0]))
    trans = np.eye(4)
    #trans[:3,3] = np.array([0,0,3])


    matrices = []
    for inp_file in all_files:

        if args.format == "json": 
            with open(os.path.join(args.input_dir,inp_file),"r") as f:
                data = json.load(f)
                mat = dict2mat(data['pose']['extrinsics'])
        elif args.format == "txt":
            with open(os.path.join(args.input_dir,inp_file),"r") as f:
                data = f.readlines()
                mat = lines2mat(data)

        matrices.append(trans@mat)
    
    # Visualize
    visualize_matrices(matrices, axis_length=0.5, axis_width=0.02)