#version 410 core

in vec3 v_position;
in vec3 v_world_position;
in vec3 v_normal;

uniform vec3 u_camera_position;

// Inserted
uniform float u_absorption;
uniform vec4 u_background_color;
uniform float u_step_size;
uniform int u_volume_type;
uniform float u_noise_scale;
uniform int u_noise_detail;

uniform vec4 u_color;
uniform vec4 u_ambient_light;

uniform vec4 u_light_color;


out vec4 FragColor;

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


vec2 intersectAABB(vec3 rayOrigin, vec3 rayDir, vec3 boxMin, vec3 boxMax) {
    vec3 tMin = (boxMin - rayOrigin) / rayDir;
    vec3 tMax = (boxMax - rayOrigin) / rayDir;
    vec3 t1 = min(tMin, tMax);
    vec3 t2 = max(tMin, tMax);
    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);
    return vec2(tNear, tFar);
};

float homogeneousRayMarching(vec2 t){
    // 3. Compute the optical thickness.
    float optical_thickness = (t.y - t.x) * u_absorption;
    return optical_thickness;
}

float heterogeneousRayMarching(vec3 ray_position, vec3 ray_direction, vec2 t){
    vec3 p;
    float optical_thickness = 0.0;
    float absorption_coeffitient = 0.2;
    for (float i=t.x; i<t.y; i+=u_step_size) {
        p = ray_position + i * ray_direction;
        absorption_coeffitient = cnoise(p, u_noise_scale, u_noise_detail);
        optical_thickness += absorption_coeffitient * u_absorption * u_step_size;
        
        // if (optical_thickness > 1.0) {
        //     vec3 bg_col = vec3(u_background_color.x, u_background_color.y, u_background_color.z);
        //     return vec4(bg_col, 1.0);
        // }
    }
    return optical_thickness;
}

vec4 computeColor (vec3 ray_position, vec3 ray_direction, vec2 t, vec3 bg_color){
    float optical_thickness = 0.0;
    float transmittance;
    vec3 color = vec3(0.0, 0.0, 0.0);
    vec3 light_color = vec3(u_color.x, u_color.y, u_color.z);
    vec3 background_color = vec3(u_background_color.x, u_background_color.y, u_background_color.z);
    vec3 p = vec3(0.0); 
    float absorption_coeffitient = cnoise(p, u_noise_scale, u_noise_detail);

    for (float i=0; i<t.y; i+=u_step_size) {
        p = ray_position + i * ray_direction;
        absorption_coeffitient = cnoise(p, u_noise_scale, u_noise_detail);
        optical_thickness += absorption_coeffitient * u_absorption * u_step_size;
        transmittance = exp(-optical_thickness);
        color += light_color * u_absorption * transmittance* u_step_size;
    };

    color += background_color * exp(-optical_thickness);
    return vec4(color, 1.0);

}


void main()
{
    // 1. Compute the ray direction.
    vec3 ray_position = u_camera_position;
    vec3 ray_direction = v_world_position - u_camera_position;

    // 2. Compute intersections with the volume auxiliary geometry.
    vec2 t = intersectAABB(ray_position, normalize(ray_direction), vec3(-1.0, -1.0, -1.0), vec3(1.0, 1.0, 1.0));
	if (t.x > t.y) {
        FragColor = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

	vec3 bg_color = vec3(u_background_color.x, u_background_color.y, u_background_color.z);
    FragColor = computeColor(ray_position, ray_direction, t, bg_color);
}
