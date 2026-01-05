"""
Generate a beautiful coffee cup icon for StandUpTracker
Requires: pip install pillow
"""
from PIL import Image, ImageDraw
import os

def create_icon():
    """Create a multi-resolution ICO file with a coffee cup design"""
    
    # Define sizes for the icon (standard Windows icon sizes)
    sizes = [256, 128, 64, 48, 32, 16]
    images = []
    
    for size in sizes:
        # Create a new image with transparency
        img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)
        
        # Calculate proportions based on size
        margin = size * 0.1
        
        # Define colors - beautiful gradient coffee brown/teal theme
        cup_color = (100, 70, 60)  # Dark brown
        coffee_color = (139, 90, 60)  # Coffee brown
        steam_color = (180, 200, 220, 180)  # Light blue-gray steam
        saucer_color = (120, 90, 80)  # Brown
        accent_color = (72, 209, 204)  # Turquoise accent
        
        # Scale factor for drawing
        s = size / 256.0
        
        # Draw saucer (ellipse at bottom)
        saucer_width = int(140 * s)
        saucer_height = int(20 * s)
        saucer_x = (size - saucer_width) // 2
        saucer_y = int(200 * s)
        draw.ellipse(
            [saucer_x, saucer_y, saucer_x + saucer_width, saucer_y + saucer_height],
            fill=saucer_color,
            outline=accent_color,
            width=max(1, int(2 * s))
        )
        
        # Draw cup body (rounded rectangle)
        cup_top = int(80 * s)
        cup_bottom = int(190 * s)
        cup_left = int(70 * s)
        cup_right = int(186 * s)
        
        # Cup body
        draw.rounded_rectangle(
            [cup_left, cup_top, cup_right, cup_bottom],
            radius=int(15 * s),
            fill=cup_color,
            outline=accent_color,
            width=max(1, int(3 * s))
        )
        
        # Coffee surface (ellipse)
        coffee_top = int(85 * s)
        coffee_height = int(15 * s)
        draw.ellipse(
            [cup_left + int(5 * s), coffee_top, 
             cup_right - int(5 * s), coffee_top + coffee_height],
            fill=coffee_color,
            outline=coffee_color
        )
        
        # Cup handle (arc on the right)
        handle_width = int(30 * s)
        handle_height = int(70 * s)
        handle_x = cup_right - int(10 * s)
        handle_y = int(100 * s)
        
        # Outer arc
        draw.arc(
            [handle_x, handle_y, handle_x + handle_width, handle_y + handle_height],
            start=270, end=90,
            fill=accent_color,
            width=max(2, int(5 * s))
        )
        
        # Inner arc (to create hollow handle effect)
        inner_offset = int(8 * s)
        draw.arc(
            [handle_x + inner_offset, handle_y + inner_offset, 
             handle_x + handle_width - inner_offset, handle_y + handle_height - inner_offset],
            start=270, end=90,
            fill=cup_color,
            width=max(2, int(4 * s))
        )
        
        # Draw steam wisps (3 wavy lines above cup)
        if size >= 32:  # Only draw steam on larger sizes
            steam_starts = [int(95 * s), int(120 * s), int(145 * s)]
            
            for i, start_x in enumerate(steam_starts):
                # Create wavy steam path
                steam_points = []
                base_y = int(40 * s)
                
                for j in range(6):
                    y = base_y + j * int(8 * s)
                    offset = int(8 * s) * (1 if (j + i) % 2 == 0 else -1)
                    steam_points.append((start_x + offset, y))
                
                # Draw the steam line
                if len(steam_points) >= 2:
                    draw.line(steam_points, fill=steam_color, width=max(1, int(3 * s)), joint="curve")
        
        # Add a highlight on the cup for depth
        highlight_x = cup_left + int(20 * s)
        highlight_y = cup_top + int(30 * s)
        highlight_width = int(15 * s)
        highlight_height = int(40 * s)
        
        draw.ellipse(
            [highlight_x, highlight_y, highlight_x + highlight_width, highlight_y + highlight_height],
            fill=(255, 255, 255, 40)  # Semi-transparent white
        )
        
        images.append(img)
    
    # Save as ICO file with multiple resolutions
    output_path = os.path.join(os.path.dirname(__file__), 'app_icon.ico')
    images[0].save(
        output_path,
        format='ICO',
        sizes=[(img.width, img.height) for img in images],
        append_images=images[1:]
    )
    
    print(f"✓ Icon created successfully: {output_path}")
    print(f"  Contains {len(sizes)} sizes: {', '.join(map(str, sizes))}px")
    
    # Also save a PNG preview of the largest size
    preview_path = os.path.join(os.path.dirname(__file__), 'app_icon_preview.png')
    images[0].save(preview_path)
    print(f"✓ Preview saved: {preview_path}")

if __name__ == '__main__':
    create_icon()
