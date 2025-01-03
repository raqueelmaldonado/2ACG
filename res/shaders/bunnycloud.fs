#version 410 core

in vec3 v_position;
in vec3 v_world_position;
in vec3 v_normal;

uniform vec3 u_camera_position;
uniform float u_absorption;
uniform vec4 u_background_color;
uniform float u_step_size;

uniform int u_density_type; // 0 = VDB File, 1 = 3D Noise, 2 = Constant density

// Emission-Absorption
uniform float u_noise_scale;
uniform int u_noise_detail;
uniform vec4 u_color;


// Lab 4
uniform sampler3D u_texture; // VDB File

out vec4 FragColor;

uniform vec4 u_light_color; // Light color and intensity
uniform float u_light_intensity; // Light intensity
uniform vec3 u_local_light_position; // Position of the light source
uniform float u_light_shininess;
uniform vec3 u_light_position;
uniform int u_light_type;
uniform float u_g;


uniform float u_scattering; // Scattering coefficient (µs)


// Noise functions
float hash1( float n )
{
    return fract( n*17.0*fract( n*0.3183099 ) );
}

float noise( vec3 x )
{
    vec3 p = floor(x);
    vec3 w = fract(x);
    
    vec3 u = w*w*w*(w*(w*6.0-15.0)+10.0);
    
    float n = p.x + 317.0*p.y + 157.0*p.z;
    
    float a = hash1(n+0.0);
    float b = hash1(n+1.0);
    float c = hash1(n+317.0);
    float d = hash1(n+318.0);
    float e = hash1(n+157.0);
    float f = hash1(n+158.0);
    float g = hash1(n+474.0);
    float h = hash1(n+475.0);

    float k0 =   a;
    float k1 =   b - a;
    float k2 =   c - a;
    float k3 =   e - a;
    float k4 =   a - b - c + d;
    float k5 =   a - c - e + g;
    float k6 =   a - b - e + f;
    float k7 = - a + b + c - d + e - f - g + h;

    return -1.0+2.0*(k0 + k1*u.x + k2*u.y + k3*u.z + k4*u.x*u.y + k5*u.y*u.z + k6*u.z*u.x + k7*u.x*u.y*u.z);
}

#define MAX_OCTAVES 16

float fractal_noise( vec3 P, float detail )
{
    float fscale = 1.0;
    float amp = 1.0;
    float sum = 0.0;
    float octaves = clamp(detail, 0.0, 16.0);
    int n = int(octaves);

    for (int i = 0; i <= MAX_OCTAVES; i++) {
        if (i > n) continue;
        float t = noise(fscale * P);
        sum += t * amp;
        amp *= 0.5;
        fscale *= 2.0;
    }

    return sum;
}

float cnoise( vec3 P, float scale, float detail )
{
    P *= scale;
    return clamp(fractal_noise(P, detail), 0.0, 1.0);
}



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

float phase_function(vec3 light_dir, vec3 view_dir) {
    float cos_theta = dot(light_dir, view_dir); // Compute the cosine of the angle between light and view directions
    float first_term = 1.0 / (4.0 * 3.14159265359); // 1 / 4π
    float numerator = 1.0 - u_g * u_g; // (1 - g^2)
    float denominator = pow(1.0 + u_g * u_g - 2.0 * u_g * cos_theta, 1.5); // (1 + g^2 - 2gcos(θ))^(3/2)
    return first_term * (numerator / denominator);
}



vec3 computeInScatteredLight(vec3 sample_position) { //compute Ls
    vec3 light_direction = normalize(u_local_light_position - sample_position);
    vec2 light_t = intersectAABB(sample_position, light_direction, vec3(-1.0), vec3(1.0));
    float density;
    if (light_t.x > light_t.y || light_t.y <= 0.0) {
        return vec3(0.0); // No contribution if no intersection
    }
    float optical_thickness = 0.0;
    vec3 accumulated_light = vec3(0.0);
    for (float i = light_t.x; i < light_t.y; i += u_step_size) {
        vec3 light_sample_position = sample_position + i * light_direction;
    
        if (u_density_type == 0) {
            // Get density from the VDB file
            vec3 textureCoord = (light_sample_position + 1.0) / 2.0; // Convert to texture coordinates
            density = texture(u_texture, textureCoord).r;
        } 
        else if (u_density_type == 1) density = cnoise(light_sample_position, u_noise_scale, u_noise_detail); // 3D noise
        else if (u_density_type == 2) density = 1.0; // Constant density
        optical_thickness += density * u_scattering * u_step_size;
        float transmittance = exp(-optical_thickness);
        accumulated_light += u_light_color.xyz * u_light_intensity * transmittance * u_step_size * u_light_shininess;
    };
    accumulated_light += u_light_color.xyz * u_light_intensity * exp(-optical_thickness) * u_light_shininess; 
    return accumulated_light;
}


vec4 computeColor (vec3 ray_position, vec3 ray_direction, vec2 t){
    // Initialize variables
    float optical_thickness = 0.0;
    vec3 accumulated_light = vec3(0.0);
    float transmittance;
    vec3 final_color = vec3(0.0, 0.0, 0.0);
    vec3 light_color = vec3(u_color.x, u_color.y, u_color.z);
    vec3 p = vec3(0.0); 
    float density;
    vec3 texture_coord;
    float scattering_term;
    vec3 scattered_color;
    float phase;
    for (float i=0; i<t.y; i+=u_step_size) {
        p = ray_position + i * ray_direction;
        if (u_density_type == 0) {
            // Get density from the VDB file
            texture_coord = (p + 1.0) / 2.0; // Convert to texture coordinates
            density = texture(u_texture, texture_coord).r;
            accumulated_light = computeInScatteredLight(p);
        } 
        else if (u_density_type == 1) density = cnoise(p, u_noise_scale, u_noise_detail); // 3D noise
        else if (u_density_type == 2) density = 1.0; // Constant density
        phase = phase_function(u_light_position, ray_direction);
        scattered_color = computeInScatteredLight(p) * phase;
        scattering_term = density * u_scattering; 
        optical_thickness += density * u_absorption * u_step_size;
        transmittance = exp(-optical_thickness);
        final_color += u_color.xyz * u_step_size * (u_absorption * transmittance + scattering_term);
    }
    final_color += u_background_color.xyz * exp(-optical_thickness);
    return vec4(final_color, 1.0);
}


void main() {
    // Compute ray direction
    vec3 ray_position = u_camera_position;
    vec3 ray_direction = normalize(v_world_position - u_camera_position);

    // Intersect the volume's bounding box
    vec2 t = intersectAABB(ray_position, ray_direction, vec3(-1.0, -1.0, -1.0), vec3(1.0, 1.0, 1.0));

    // Check for valid intersection
    if (t.x > t.y || t.y <= 0.0) {
        FragColor = u_background_color;
        return;
    }
    // Compute final color
    FragColor = computeColor(ray_position, ray_direction, t);
}