#version 410 core

in vec3 a_vertex;
in vec3 a_normal;
in vec4 a_color;
in vec2 a_uv;

uniform mat4 u_model;
uniform mat4 u_viewprojection;
uniform vec3 u_camera_position;

//this will store the color for the pixel shader
out vec3 v_position;
out vec3 v_world_position;

void main()
{		
	//calcule the vertex in object space
	v_position = a_vertex;
	v_world_position = (u_model * vec4( v_position, 1.0) ).xyz;

	//calcule the position of the vertex using the matrices
	gl_Position = u_viewprojection * vec4( v_world_position, 1.0 );
}