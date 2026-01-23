import numpy as np
from plyfile import PlyData, PlyElement
import os
from glob import glob
from tqdm import tqdm

def merge_ply_meshes(file_paths, output_file_path):
    """
    Reads vertex and face data from multiple PLY files, concatenates the vertices,
    re-indexes the faces, and writes the result to a single output PLY file.

    Args:
        file_paths (list): A list of paths to the input PLY files.
        output_file_path (str): The path to save the merged PLY file.
    """
    if not file_paths:
        print("Error: No input files provided.")
        return

    file_paths = file_paths[::-1]
    combined_vertex_data = []
    combined_face_data = []
    total_vertices_merged = 0
    
    # Store the property names from the first file to ensure consistency
    vertex_property_names = None
    face_dtype = None # Will store the expected dtype for faces (e.g., [('vertex_indices', 'i4', (3,))])

    print("Reading and combining PLY files...")

    first = True
    num_merged = 0

    for i, file_path in tqdm(enumerate(file_paths),total=len(file_paths)):
        if not os.path.exists(file_path):
            print(f"Warning: File not found: {file_path}. Skipping.")
            continue
            
        try:
            ply = PlyData.read(file_path)
            if len(ply['vertex'].data) == 0:
                continue
            
            # --- 1. Process Vertex Data ---
            if 'vertex' not in ply:
                print(f"Warning: '{file_path}' has no 'vertex' element. Skipping file.")
                continue

            vertex_data = ply['vertex'].data
            
            if first:
                # Capture properties from the first file
                vertex_property_names = vertex_data.dtype.names
            else:
                # Ensure subsequent files match the first file's properties
                if vertex_data.dtype.names != vertex_property_names:
                    print(f"Error: Vertex properties in '{file_path}' do not match the first file. Stopping merge.")
                    return

            combined_vertex_data.append(vertex_data)

            # --- 2. Process Face Data (and re-index) ---
            if 'face' in ply:
                face_data = ply['face'].data
                if first:
                    # Capture the expected data type for faces (for consistency check)
                    face_dtype = face_data.dtype
                    # No re-indexing needed for the first file
                    combined_face_data.append(face_data)
                    first = False
                else:
                    # Consistency check for face dtype
                    if face_data.dtype != face_dtype:
                        print(f"Error: Face structure/properties in '{file_path}' do not match the first file. Stopping merge.")
                        return

                    # RE-INDEXING STEP: Add the count of all previously merged vertices 
                    # to every face index in the current file.
                    
                    # Create a new array for the re-indexed faces
                    reindexed_faces = face_data.copy()
                    
                    # Access the 'vertex_indices' array (the actual list of indices)
                    # and add the offset (total_vertices_merged) to every index.
                    # This is the core logic for mesh merging.
                    reindexed_faces['vertex_indices'] = [
                        np.array(indices) + total_vertices_merged 
                        for indices in face_data['vertex_indices']
                    ]
                    
                    combined_face_data.append(reindexed_faces)

                #print(f"  Read {len(vertex_data)} vertices and {len(face_data)} faces from {os.path.basename(file_path)}")
                pass
            else:
                #print(f"  Read {len(vertex_data)} vertices from {os.path.basename(file_path)} (No faces found)")
                pass

            # Crucial update: Keep track of the total number of vertices added so far
            total_vertices_merged += len(vertex_data)
            num_merged += 1

        except Exception as e:
            print(f"Error processing PLY file '{file_path}': {e}. Skipping.")
            continue

    if not combined_vertex_data:
        print("Error: No valid data was read to merge.")
        return

    # --- 3. Concatenate the data ---
    merged_vertex_data = np.concatenate(combined_vertex_data)
    merged_face_data = np.concatenate(combined_face_data) if combined_face_data else None

    # --- 4. Create PlyElements and write the final file ---
    elements = []
    
    # Vertex element is always added
    elements.append(PlyElement.describe(merged_vertex_data, name='vertex'))

    # Face element is added only if we successfully merged faces
    if merged_face_data is not None:
        elements.append(PlyElement.describe(merged_face_data, name='face'))
        total_faces = len(merged_face_data)
    else:
        total_faces = 0

    merged_ply = PlyData(elements, text=False) # Write as binary

    merged_ply.write(output_file_path)

    print("-" * 40)
    print(f"✅ Successfully merged {len(merged_vertex_data)} vertices and {total_faces} faces from {num_merged} files.")
    print(f"Output file saved to: **{output_file_path}**")

# --- Usage Example (Same setup as before, ensure your files have face data) ---
if __name__ == '__main__':
    # Define the directory where your PLY files are located
    #input_directory = './input_ply_files_with_faces' 
    input_directory = '/home/sotka/Downloads/reset_meshes' 
    output_filename = 'run2_mesh.ply'

    # Create dummy files for testing if the directory doesn't exist
    if not os.path.exists(input_directory):
        os.makedirs(input_directory)
        print(f"Created directory: {input_directory}")

        # Define vertex and face data types
        v_dtype = [('x', 'f4'), ('y', 'f4'), ('z', 'f4')]
        f_dtype = [('vertex_indices', 'i4', (3,))] # Triangles

        # File 1: Simple quad (2 triangles, 4 vertices)
        v1_data = np.array([(0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (1.0, 1.0, 0.0), (0.0, 1.0, 0.0)], dtype=v_dtype)
        # Faces: [0, 1, 2] and [0, 2, 3]
        f1_data = np.array([([0, 1, 2],), ([0, 2, 3],)], dtype=f_dtype) 
        
        el_v1 = PlyElement.describe(v1_data, 'vertex')
        el_f1 = PlyElement.describe(f1_data, 'face')
        PlyData([el_v1, el_f1]).write(os.path.join(input_directory, 'mesh_part_a.ply'))

        # File 2: Separate triangle (3 vertices)
        v2_data = np.array([(2.0, 2.0, 0.0), (3.0, 2.0, 0.0), (2.5, 3.0, 0.0)], dtype=v_dtype)
        # Faces: [0, 1, 2] (These will be re-indexed to [4, 5, 6] in the merge)
        f2_data = np.array([([0, 1, 2],)], dtype=f_dtype) 

        el_v2 = PlyElement.describe(v2_data, 'vertex')
        el_f2 = PlyElement.describe(f2_data, 'face')
        PlyData([el_v2, el_f2]).write(os.path.join(input_directory, 'mesh_part_b.ply'))
        
        print("Created two dummy PLY mesh files for demonstration.")
    
    # Use glob to find all .ply files in the input directory
    ply_files_to_merge = sorted(glob(os.path.join(input_directory, '*.ply')))

    # Filtering with keywords
    ply_files_to_merge = [ply for ply in ply_files_to_merge if 'Ceiling' not in ply]
    
    # Run the merge function
    merge_ply_meshes(
        file_paths=ply_files_to_merge, 
        output_file_path=output_filename,
    )