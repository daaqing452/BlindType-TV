package touchInput;

import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.net.Socket;
import java.util.ArrayList;
import java.util.Timer;
import java.util.TimerTask;

import com.example.eftei.R;

import android.app.Activity;
import android.os.AsyncTask;
import android.os.Bundle;
import android.util.DisplayMetrics;
import android.view.MotionEvent;
import android.view.View;
import android.view.View.OnClickListener;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;

public class MainActivity extends Activity {

	static int connectPort = 10309;

	int width;
	int height;
	
	Socket socket = null;
	Button clearButton, okButton;
	EditText ipEditText;
	PrintWriter printWriter;
	TextView infoTextView;
	
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		setContentView(R.layout.activity_main);
		infoTextView = (TextView)findViewById(R.id.infoTextView);
		okButton = (Button)findViewById(R.id.okButton);
		okButton.setOnClickListener(okButtonOnClickListener);
		ipEditText = (EditText)findViewById(R.id.ipEditText);
		
		DisplayMetrics displayMetrics = new DisplayMetrics();
		getWindowManager().getDefaultDisplay().getMetrics(displayMetrics);
		width = displayMetrics.widthPixels;
		height = displayMetrics.heightPixels;
	}
	
	OnClickListener okButtonOnClickListener = new OnClickListener() {
		public void onClick(View v) {
			if (socket == null) {
				new NetworkAsyncTask().execute(ipEditText.getText().toString());
			} else {
				socket = null;
				infoTextView.setText("Hello");
			}
		}
	};
	
	final int HOLDDOWN_TIME = 500;
	final int STAY_DIST = 60;
	final int SLIP_DIST = 80;
	
	final int MODE_NONE = 0;
	final int MODE_CLICK = 1;
	final int MODE_DRAG = 2;
	
	int n;
	ArrayList<TouchEvent> touchEventList = new ArrayList<TouchEvent>();
	int moveX = 0, moveY = 0;
	int mode = 0;
	
	public boolean onTouchEvent(MotionEvent event) {
		
		if (socket == null) return super.onTouchEvent(event);
		n = event.getActionIndex();
		int x = (int)event.getX();
		int y = (int)event.getY();
		
		printWriter.write(n + " " + event.getAction() + " (" + event.getX(n) + " " + event.getY(n) + ")\n");
		printWriter.flush();
		
		switch (event.getAction() & MotionEvent.ACTION_MASK) {
		
		case MotionEvent.ACTION_DOWN:
		case MotionEvent.ACTION_POINTER_DOWN:
			touchEventList.add(new TouchEvent(x, y));
			mode = MODE_CLICK;
			break;
			
		case MotionEvent.ACTION_MOVE:
			switch (mode) {
			case MODE_CLICK:
				moveX = x;
				moveY = y;
				break;
			case MODE_DRAG:
				printWriter.write("drag " + x + " " + y + "\n");
				printWriter.flush();
				break;
			}
			break;
		
		case MotionEvent.ACTION_UP:
		case MotionEvent.ACTION_POINTER_UP:
			TouchEvent touchEvent = touchEventList.get(n);
			switch (mode) {
			case MODE_CLICK:
				//	point
				if (ifStay(touchEvent.downX, touchEvent.downY, x, y)) {
					int midx = (x + touchEvent.downX) / 2;
					int midy = (y + touchEvent.downY) / 2;
					printWriter.write("click " + midx + " " + midy + "\n");
					printWriter.flush();
				}
				
				//	left slip
				if (x < touchEvent.downX - SLIP_DIST) {
					printWriter.write("leftslip\n");
					printWriter.flush();
				}
				
				//	right slip
				if (x > touchEvent.downX + SLIP_DIST) {
					printWriter.write("rightslip\n");
					printWriter.flush();
				}
				break;
				
			case MODE_DRAG:
				//	drag
				printWriter.write("dragend " + x + " " + y + "\n");
				printWriter.flush();
				break;
			}
			mode = MODE_NONE;
			touchEvent.cancel();
			touchEventList.remove(n);
			break;
		}
		
		return super.onTouchEvent(event);
	}
	
	class TouchEvent {
		public int downX, downY;
		public Timer dragTimer;
		
		public TouchEvent(int downX2, int downY2) {
			downX = downX2;
			downY = downY2;
			dragTimer = new Timer();
			dragTimer.schedule(new TimerTask() {
				public void run() {
					if (ifStay(moveX, moveY, downX, downY)) {
						mode = MODE_DRAG;
						printWriter.write("dragbegin " + moveX + " " + moveY + "\n");
						printWriter.flush();
					}
				}
			}, HOLDDOWN_TIME);
		}
		
		public void cancel() {
			dragTimer.cancel();
		}
	}
	
	class NetworkAsyncTask extends AsyncTask<String, Integer, String> {
		
		protected String doInBackground(String... params) {
			try {
				socket = new Socket(params[0], connectPort);
				printWriter = new PrintWriter(new OutputStreamWriter(socket.getOutputStream()));
				Thread.sleep(300);
				printWriter.write("devicesize " + width + " " + height + "\n");
				printWriter.flush();
				return socket.toString();
			} catch (Exception e) {
				socket = null;
				return e.toString();
			}
		}
		
		protected void onPostExecute(String string) {
			infoTextView.setText(string);
		}
	}
	
	boolean ifStay(int x0, int y0, int x1, int y1) {
		return Math.sqrt(Math.pow(x0 - x1, 2) + Math.pow(y0 - y1, 2)) < STAY_DIST;
	}
}
