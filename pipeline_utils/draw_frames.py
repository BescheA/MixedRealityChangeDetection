import viser
import numpy as np
from scipy.spatial.transform import Rotation as R
import json
import argparse
import os

def dict2mat(matdict):
    mat = np.zeros((4,4))
    for i in range(4):
        for j in range(4):
            mat[i,j] = matdict[f"e{i}{j}"]

    return mat

def create_sample_matrices():
    """Create a list of sample 4x4 transformation matrices."""
    matrices = []
    
    # Identity matrix
    matrices.append(np.eye(4))
    
    # Translation matrices
    for i in range(3):
        mat = np.eye(4)
        mat[:3, 3] = [i * 2, 0, 0]
        matrices.append(mat)
    
    # Rotation matrices (around Z axis)
    for angle in [0, 45, 90, 135]:
        mat = np.eye(4)
        rot = R.from_euler('z', angle, degrees=True)
        mat[:3, :3] = rot.as_matrix()
        mat[:3, 3] = [0, angle / 45 * 2, 0]
        matrices.append(mat)
    
    # Combined rotation and translation
    for i in range(4):
        mat = np.eye(4)
        rot = R.from_euler('xyz', [30 * i, 45, 60], degrees=True)
        mat[:3, :3] = rot.as_matrix()
        mat[:3, 3] = [i * 1.5, i * 1.5, i * 0.5]
        matrices.append(mat)
    
    return matrices

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
        quat_xyzw = rot.as_quat()  # scipy gives xyzw
        quat_wxyz = np.array([quat_xyzw[3], quat_xyzw[0], quat_xyzw[1], quat_xyzw[2]])
        
        # Add coordinate frame
        server.scene.add_frame(
            name=f"/frame_{idx}",
            wxyz=quat_wxyz,
            position=position,
            axes_length=axis_length,
            axes_radius=axis_width,
        )
        
        # Add a label
        #server.scene.add_label(
        #    name=f"/label_{idx}",
        #    text=f"Frame {idx}",
        #    position=position + np.array([0, 0, axis_length + 0.1]),
        #)
        
        # Optionally add a small sphere at the origin
        #server.scene.add_icosphere(
        #    name=f"/sphere_{idx}",
        #    radius=0.05,
        #    position=position,
        #    color=(100, 150, 255),
        #)
    
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

    args = parser.parse_args()

    # Create sample matrices
    #matrices = create_sample_matrices()

    json_files = os.listdir(args.input_dir)

    matrices = []
    for json_file in json_files:
        with open(os.path.join(args.input_dir,json_file),"r") as f:
            data = json.load(f)
        
        matrices.append(dict2mat(data['pose']['extrinsics']))
    
    # Visualize
    visualize_matrices(matrices, axis_length=0.5, axis_width=0.02)