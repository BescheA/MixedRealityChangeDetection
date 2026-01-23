import os
import matplotlib.pyplot as plt
import numpy as np
from PIL import Image

def interact_depth_map(image_path):
    # Load the TIFF image
    try:
        img = Image.open(image_path)
        depth_array = np.array(img)
    except Exception as e:
        print(f"Error loading image: {e}")
        return

    fig, ax = plt.subplots(figsize=(10, 8))
    
    # Use 'magma' or 'viridis' for better depth perception
    # vmin and vmax can be adjusted if the image looks too dark/bright
    im = ax.imshow(depth_array, cmap='magma')
    plt.colorbar(im, label='Depth Value')
    ax.set_title(f"Depth Map: {image_path}\nClick to see depth value")

    # This function is called every time you click
    def onclick(event):
        if event.xdata is not None and event.ydata is not None:
            # Convert float coordinates to integer indices
            x, y = int(round(event.xdata)), int(round(event.ydata))
            
            # Ensure the click is within image bounds
            if 0 <= x < depth_array.shape[1] and 0 <= y < depth_array.shape[0]:
                value = depth_array[y, x]
                print(f"Location: (x={x}, y={y}) | Depth Value: {value}")
                
                # Optional: Add a temporary marker at the click location
                ax.plot(x, y, 'gs', markersize=5) 
                plt.draw()

    # Connect the click event to our function
    cid = fig.canvas.mpl_connect('button_press_event', onclick)

    plt.show()

#replace with filepath of file 
filepath1 = os.path.join('room1','unidepths',"000045_depth.tiff")
interact_depth_map(filepath1)