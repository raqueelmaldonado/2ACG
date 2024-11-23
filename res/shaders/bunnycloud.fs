#version 410 core

in vec3 v_position;
in vec3 v_world_position;
in vec3 v_normal;

uniform vec3 u_camera_position;
uniform float u_absorption;
uniform vec4 u_background_color;
uniform float u_step_size;
uniform int u_volume_type; // 0 = Homogeneous, 1 = Heterogeneous
uniform sampler3D u_texture;

out vec4 FragColor;

// Ray-AABB intersection
vec2 intersectAABB(vec3 rayOrigin, vec3 rayDir, vec3 boxMin, vec3 boxMax) {
    vec3 tMin = (boxMin - rayOrigin) / rayDir;
    vec3 tMax = (boxMax - rayOrigin) / rayDir;
    vec3 t1 = min(tMin, tMax);
    vec3 t2 = max(tMin, tMax);
    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);
    return vec2(tNear, tFar);
}

// Homogeneous ray marching
float homogeneousRayMarching(vec2 t) {
    return (t.y - t.x) * u_absorption;
}

// Heterogeneous ray marching
float heterogeneousRayMarching(vec3 ray_position, vec3 ray_direction, vec2 t, sampler3D volumeTexture) {
    vec3 p;
    float optical_thickness = 0.0;

    for (float i = t.x; i < t.y; i += u_step_size) {
        // Compute the current sample position
        p = ray_position + i * ray_direction;

        // Convert local position to texture coordinates
        vec3 textureCoord = (p + 1.0) / 2.0;

        // Sample the 3D texture and extract the red channel
        float absorption_coeffitient = texture(volumeTexture, textureCoord).r;

        // Accumulate optical thickness
        optical_thickness += absorption_coeffitient * u_absorption * u_step_size;
    }

    return optical_thickness;
}

void main() {
    // Compute ray direction
    vec3 ray_position = u_camera_position;
    vec3 ray_direction = normalize(v_world_position - u_camera_position);

    // Intersect the volume's bounding box
    vec2 t = intersectAABB(ray_position, ray_direction, vec3(-1.0, -1.0, -1.0), vec3(1.0, 1.0, 1.0));

    // Check for valid intersection
    if (t.x > t.y || t.y < 0.0) {
        FragColor = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    // Clamp t to positive values
    t.x = max(t.x, 0.0);

    // Compute optical thickness
    float optical_thickness = 0.0;
    if (u_volume_type == 0) {
        optical_thickness = homogeneousRayMarching(t);
    } else if (u_volume_type == 1) {
        optical_thickness = heterogeneousRayMarching(ray_position, ray_direction, t, u_texture);
    }

    // Compute transmittance
    float transmittance = exp(-optical_thickness);

    // Compute final color
    vec3 bg_col = u_background_color.rgb;
    FragColor = vec4(bg_col * transmittance, 1.0);
}
